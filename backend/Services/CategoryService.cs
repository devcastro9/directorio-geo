using backend.Data;
using backend.DTOs;
using backend.DTOs.Categories;
using backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Services;

public sealed class CategoryService(AppDbContext db, ILogger<CategoryService> logger) : ICategoryService
{
    private static readonly Expression<Func<Category, CategoryDto>> Projection = c => new(
        c.Id, c.Code, c.Name, c.Description, c.ParentId, c.SortOrder, c.Active);

    private IQueryable<Category> Visible => db.Categories.Where(c => c.Active);

    public async Task<PagedResult<CategoryDto>> ListAsync(ListRequest request, CancellationToken ct = default)
    {
        // Validar exclusividad de filtros de jerarquía
        if (request.RootOnly && request.ParentId.HasValue)
        {
            throw new BadHttpRequestException(
                "Los parámetros 'rootOnly' y 'parentId' son mutuamente excluyentes.",
                StatusCodes.Status400BadRequest);
        }

        IQueryable<Category> query = Visible.AsNoTracking();

        // Filtro de jerarquía
        if (request.RootOnly)
        {
            query = query.Where(c => c.ParentId == null);
        }
        else if (request.ParentId.HasValue)
        {
            query = query.Where(c => c.ParentId == request.ParentId.Value);
        }
        // Búsqueda textual
        string? search = request.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            string pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(c => EF.Functions.ILike(c.Name, pattern, "\\")
                || EF.Functions.ILike(c.Code, pattern, "\\"));
        }

        int total = await query.CountAsync(ct);
        long offset = ((long)request.Page - 1) * request.PageSize;
        List<CategoryDto> items = offset >= total
            ? []
            : await query
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id)
                .Skip((int)offset).Take(request.PageSize)
                .Select(Projection)
                .ToListAsync(ct);

        logger.LogDebug(
            "Querying categories: page {Page}, total {Total}, rootOnly={RootOnly}, parentId={ParentId}.",
            request.Page, total, request.RootOnly, request.ParentId);
        return new(items, request.Page, request.PageSize, total);
    }

    public async Task<CategoryDto> GetAsync(Guid id, CancellationToken ct = default) =>
        await Visible.AsNoTracking().Where(c => c.Id == id).Select(Projection).SingleOrDefaultAsync(ct)
        ?? throw new BadHttpRequestException("La categoría no fue encontrada.", StatusCodes.Status404NotFound);

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            await ValidateParentAsync(request.ParentId, null, ct);

            var category = new Category
            {
                Id = Guid.CreateVersion7(),
                Code = request.Code.Trim(),
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                ParentId = request.ParentId,
                SortOrder = request.SortOrder,
                Active = true
            };
            db.Categories.Add(category);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            logger.LogInformation("Category {CategoryId} created.", category.Id);
            return await Visible.AsNoTracking().Where(c => c.Id == category.Id).Select(Projection).SingleAsync(ct);
        }
        catch (Exception ex) when (ex is DbUpdateException or PostgresException)
        {
            throw TranslatePostgresException(ex);
        }
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            Category category = await FindAsync(id, ct);
            await ValidateParentAsync(request.ParentId, id, ct);

            category.Code = request.Code.Trim();
            category.Name = request.Name.Trim();
            category.Description = request.Description?.Trim();
            category.ParentId = request.ParentId;
            category.SortOrder = request.SortOrder;
            category.Active = request.IsActive;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            logger.LogInformation("Category {CategoryId} updated.", id);

            // La proyección usa Visible (Active && DeletedAt==null).
            // Si se desactivó, re-leer sin el filtro Active para devolver el estado actual.
            CategoryDto? dto = await db.Categories
                .AsNoTracking()
                .Where(c => c.Id == id && c.DeletedAt == null)
                .Select(Projection)
                .SingleOrDefaultAsync(ct);

            return dto ?? throw new BadHttpRequestException("La categoría no fue encontrada.", StatusCodes.Status404NotFound);
        }
        catch (Exception ex) when (ex is DbUpdateException or PostgresException)
        {
            throw TranslatePostgresException(ex);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            Category category = await FindAsync(id, ct);

            if (await db.Categories.AnyAsync(c => c.ParentId == id && c.Active, ct))
            {
                throw new BadHttpRequestException(
                    "Elimina o reasigna las subcategorías hijas antes de eliminar esta categoría.",
                    StatusCodes.Status409Conflict);
            }
            category.Active = false;
            // deleted_at y deleted_by_user_id los gestiona el trigger de BD.
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            logger.LogInformation("Category {CategoryId} soft-deleted.", id);
        }
        catch (Exception ex) when (ex is DbUpdateException or PostgresException)
        {
            throw TranslatePostgresException(ex);
        }
    }

    private async Task<Category> FindAsync(Guid id, CancellationToken ct) =>
        await Visible.SingleOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new BadHttpRequestException("La categoría no fue encontrada.", StatusCodes.Status404NotFound);

    private async Task ValidateParentAsync(Guid? parentId, Guid? id, CancellationToken ct)
    {
        var visited = new HashSet<Guid>();
        Guid? current = parentId;
        while (current.HasValue)
        {
            if (current == id || !visited.Add(current.Value))
                throw new BadHttpRequestException(
                    "La categoría padre generaría un ciclo en la jerarquía.",
                    StatusCodes.Status409Conflict);

            var parent = await Visible.AsNoTracking()
                .Where(c => c.Id == current.Value)
                .Select(c => new { c.ParentId })
                .SingleOrDefaultAsync(ct);

            if (parent is null)
                throw new ValidationException(
                    new ValidationResult("La categoría padre debe existir y estar activa.", ["ParentId"]),
                    null, parentId);

            current = parent.ParentId;
        }
    }

    private Exception TranslatePostgresException(Exception ex)
    {
        PostgresException? pg = ex as PostgresException ?? ex.InnerException as PostgresException;
        logger.LogWarning("Category write rejected by PostgreSQL: {SqlState}.", pg?.SqlState);

        return pg?.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation =>
                new BadHttpRequestException("Ya existe una categoría con ese código.", StatusCodes.Status409Conflict),
            PostgresErrorCodes.ForeignKeyViolation =>
                new BadHttpRequestException("La relación de categoría cambió. Verifica la categoría padre.", StatusCodes.Status409Conflict),
            PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected =>
                new BadHttpRequestException("El catálogo cambió durante la operación. Intenta nuevamente.", StatusCodes.Status409Conflict),
            PostgresErrorCodes.RaiseException =>
                new BadHttpRequestException("La categoría padre es incompatible con la jerarquía.", StatusCodes.Status409Conflict),
            _ => ex
        };
    }
}
