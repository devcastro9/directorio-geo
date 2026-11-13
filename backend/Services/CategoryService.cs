using backend.Data;
using backend.DTOs;
using backend.DTOs.Categories;
using backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
        c.Id, c.Code, c.Name, c.Description, c.ParentId, c.SortOrder,
        c.Active, c.CreatedAt, c.UpdatedAt);

    private IQueryable<Category> Visible => db.Categories.Where(c => c.Active && c.DeletedAt == null);

    public async Task<PagedResult<CategoryDto>> ListAsync(ListRequest request, CancellationToken ct = default)
    {
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
        IQueryable<Category> query = Visible.AsNoTracking();
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
            ? new List<CategoryDto>()
            : await query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id)
                .Skip((int)offset).Take(request.PageSize).Select(Projection).ToListAsync(ct);
        logger.LogDebug("Consulta de categorías: página {Page}, total {Total}.", request.Page, total);
        return new(items, request.Page, request.PageSize, total);
    }

    public async Task<CategoryDto> GetAsync(Guid id, CancellationToken ct = default) =>
        await Visible.AsNoTracking().Where(c => c.Id == id).Select(Projection).SingleOrDefaultAsync(ct)
        ?? throw new BadHttpRequestException("The category was not found.", StatusCodes.Status404NotFound);

    public Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default) =>
        MutateAsync(async () =>
        {
            Validate(request);
            await ValidateCodeAsync(request.Code.Trim(), null, ct);
            await ValidateParentAsync(request.ParentId, null, ct);
            var category = new Category
            {
                Id = Guid.CreateVersion7(),
                Code = request.Code.Trim(),
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                ParentId = request.ParentId,
                SortOrder = request.SortOrder,
                Active = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Categories.Add(category);
            await db.SaveChangesAsync(ct);
            // El trigger de PostgreSQL puede modificar las fechas.
            await db.Entry(category).ReloadAsync(ct);
            logger.LogInformation("Categoría {CategoryId} creada.", category.Id);
            return Map(category);
        }, ct);

    public Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default) =>
        MutateAsync(async () =>
        {
            Validate(request);
            Category category = await FindAsync(id, ct);
            await ValidateCodeAsync(request.Code.Trim(), id, ct);
            await ValidateParentAsync(request.ParentId, id, ct);
            category.Code = request.Code.Trim();
            category.Name = request.Name.Trim();
            category.Description = request.Description?.Trim();
            category.ParentId = request.ParentId;
            category.SortOrder = request.SortOrder;
            category.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await db.Entry(category).ReloadAsync(ct);
            logger.LogInformation("Categoría {CategoryId} actualizada.", id);
            return Map(category);
        }, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await MutateAsync(async () =>
        {
            Category category = await FindAsync(id, ct);
            if (await db.Categories.AnyAsync(c => c.ParentId == id && c.DeletedAt == null, ct))
                throw new BadHttpRequestException("Delete or reassign child categories before deleting this category.", StatusCodes.Status409Conflict);
            category.Active = false;
            category.DeletedAt = DateTime.UtcNow;
            category.UpdatedAt = category.DeletedAt.Value;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Categoría {CategoryId} eliminada de forma lógica.", id);
            return true;
        }, ct);
    }

    private async Task<Category> FindAsync(Guid id, CancellationToken ct) =>
        await Visible.SingleOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new BadHttpRequestException("The category was not found.", StatusCodes.Status404NotFound);

    private async Task ValidateCodeAsync(string code, Guid? id, CancellationToken ct)
    {
        // La restricción UNIQUE incluye también categorías eliminadas.
        if (await db.Categories.AnyAsync(c => c.Code == code && (!id.HasValue || c.Id != id.Value), ct))
            throw new BadHttpRequestException("A category with this code already exists, including deleted categories.", StatusCodes.Status409Conflict);
    }

    private async Task ValidateParentAsync(Guid? parentId, Guid? id, CancellationToken ct)
    {
        var visited = new HashSet<Guid>();
        Guid? current = parentId;
        while (current.HasValue)
        {
            if (current == id || !visited.Add(current.Value))
                throw new BadHttpRequestException("The parent category would create a cycle in the hierarchy.", StatusCodes.Status409Conflict);
            var parent = await Visible.AsNoTracking().Where(c => c.Id == current.Value)
                .Select(c => new { c.ParentId }).SingleOrDefaultAsync(ct);
            if (parent is null)
                throw new ValidationException(new ValidationResult(
                    "The parent category must exist and be active.", new[] { "ParentId" }), null, parentId);
            current = parent.ParentId;
        }
    }

    private static void Validate(CreateCategoryRequest request) =>
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

    private async Task<T> MutateAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        try
        {
            // Evita ciclos y bajas de padres con hijas creadas concurrentemente.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            T result = await action();
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (Exception ex) when (ex is DbUpdateException || ex is PostgresException)
        {
            PostgresException? postgres = ex as PostgresException ?? ex.InnerException as PostgresException;
            logger.LogWarning("Escritura de categoría rechazada por PostgreSQL: {SqlState}.", postgres?.SqlState);
            Exception? translated = postgres?.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation => new BadHttpRequestException("A category with this code already exists.", StatusCodes.Status409Conflict),
                PostgresErrorCodes.ForeignKeyViolation => new BadHttpRequestException("The category relationship changed. Check the parent category.", StatusCodes.Status409Conflict),
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected =>
                    new BadHttpRequestException("The catalog changed during the operation. Try again.", StatusCodes.Status409Conflict),
                PostgresErrorCodes.RaiseException => new BadHttpRequestException("The parent category is incompatible with the hierarchy.", StatusCodes.Status409Conflict),
                _ => null
            };
            if (translated is null)
                throw;
            throw translated;
        }
    }

    private static CategoryDto Map(Category c) => new(
        c.Id, c.Code, c.Name, c.Description, c.ParentId, c.SortOrder,
        c.Active, c.CreatedAt, c.UpdatedAt);
}
