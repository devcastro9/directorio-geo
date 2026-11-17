using backend.Data;
using backend.DTOs;
using backend.DTOs.Businesses;
using backend.DTOs.Management;
using backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Services;

public sealed class BusinessService(AppDbContext db, ILogger<BusinessService> logger) : IBusinessService
{
    private static readonly GeometryFactory GeometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(4326);

    public async Task<PagedResult<ManagedSummaryDto>> ListAsync(ManagementListRequest request, CancellationToken ct = default)
    {
        long offset = ((long)request.Page - 1) * request.PageSize;
        if (offset > 2000)
        {
            throw new BadHttpRequestException(
                "El desplazamiento de paginación excede el límite máximo permitido de 2000.",
                StatusCodes.Status400BadRequest);
        }

        IQueryable<Business> query = db.Businesses.AsNoTracking();

        if (request.Deleted)
        {
            query = query.Where(b => b.DeletedAt != null || b.Status == "archived");
        }
        else
        {
            query = query.Where(b => b.DeletedAt == null && b.Status != "archived");
        }

        if (!string.IsNullOrWhiteSpace(request.PublicationStatus))
        {
            string status = request.PublicationStatus.Trim().ToLowerInvariant();
            query = query.Where(b => b.Status == status);
        }

        string? q = request.Q?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            string pattern = "%" + q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            query = query.Where(b =>
                EF.Functions.ILike(b.TradeName, pattern, "\\") ||
                (b.Slug != null && EF.Functions.ILike(b.Slug, pattern, "\\")));
        }

        int total = await query.CountAsync(ct);
        if (offset >= total)
        {
            return new PagedResult<ManagedSummaryDto>([], request.Page, request.PageSize, total);
        }

        var pagedBusinesses = await query
            .OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt)
            .ThenBy(b => b.Id)
            .Skip((int)offset)
            .Take(request.PageSize)
            .Select(b => new
            {
                b.Id,
                Slug = b.Slug ?? string.Empty,
                b.TradeName,
                b.Status,
                b.DeletedAt,
                UpdatedAt = b.UpdatedAt ?? b.CreatedAt,
                BranchCount = b.Branches.Count(br => br.DeletedAt == null)
            })
            .ToListAsync(ct);

        var items = pagedBusinesses.Select(b => new ManagedSummaryDto(
            b.Id,
            b.Slug,
            b.TradeName,
            b.Status,
            b.Status,
            b.DeletedAt.HasValue ? new DateTimeOffset(b.DeletedAt.Value, TimeSpan.Zero) : null,
            new DateTimeOffset(b.UpdatedAt, TimeSpan.Zero),
            b.BranchCount
        )).ToList();

        return new PagedResult<ManagedSummaryDto>(items, request.Page, request.PageSize, total);
    }

    public async Task<ManagedBusinessDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        Business? business = await db.Businesses.AsNoTracking()
            .Include(b => b.Branches)
                .ThenInclude(br => br.BranchCategories.Where(bc => bc.Active))
            .Include(b => b.Branches)
                .ThenInclude(br => br.BranchContacts.Where(c => c.Active && c.DeletedAt == null))
            .Include(b => b.Branches)
                .ThenInclude(br => br.OpeningHours.Where(oh => oh.Active))
            .Include(b => b.Branches)
                .ThenInclude(br => br.BranchHourOverrides.Where(ho => ho.Active && ho.DeletedAt == null))
            .Include(b => b.Branches)
                .ThenInclude(br => br.BranchPhotos.Where(p => p.Active && p.DeletedAt == null))
            .SingleOrDefaultAsync(b => b.Id == id, ct);

        if (business is null)
        {
            throw new BadHttpRequestException("El negocio no fue encontrado.", StatusCodes.Status404NotFound);
        }

        return MapToManagedBusinessDto(business);
    }

    public async Task<ManagedBusinessDto> CreateAsync(CreateBusinessRequest request, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        string baseSlug = GenerateSlugBase(request.Business.TradeName);
        string slug = baseSlug;
        if (await db.Businesses.AnyAsync(b => b.Slug == slug, ct))
        {
            string hexSuffix = Guid.CreateVersion7().ToString("N")[..12];
            slug = $"{baseSlug}-{hexSuffix}";
            if (slug.Length > 160) slug = slug[..160].TrimEnd('-');
        }

        DateTime now = DateTime.UtcNow;
        var business = new Business
        {
            Id = Guid.CreateVersion7(),
            Slug = slug,
            TradeName = request.Business.TradeName.Trim(),
            Description = request.Business.Description?.Trim(),
            Status = "draft",
            LegalName = request.Business.LegalName?.Trim(),
            TaxId = request.Business.TaxId?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Businesses.Add(business);

        var branchLocation = GeometryFactory.CreatePoint(
            new Coordinate(request.Branch.Location.Coordinates[0], request.Branch.Location.Coordinates[1]));

        var branch = new Branch
        {
            Id = Guid.CreateVersion7(),
            BusinessId = business.Id,
            Name = request.Branch.Name.Trim(),
            Address = request.Branch.Address.Trim(),
            TerritoryId = request.Branch.TerritoryId,
            Location = branchLocation,
            Status = request.Branch.Status == "temporarily_closed" ? "temporarily_closed" : "active",
            IsPrimary = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Branches.Add(branch);

        AddBranchCollections(branch.Id, request.Branch, now);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Business {BusinessId} created with slug '{Slug}'.", business.Id, business.Slug);
        return await GetAsync(business.Id, ct);
    }

    public async Task<ManagedBusinessDto> UpdateAsync(Guid id, BusinessInputDto input, CancellationToken ct = default)
    {
        Business? business = await db.Businesses.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (business is null || business.DeletedAt != null || business.Status == "archived")
        {
            throw new BadHttpRequestException("El negocio no existe o está eliminado.", StatusCodes.Status409Conflict);
        }

        business.TradeName = input.TradeName.Trim();
        business.Description = input.Description?.Trim();
        business.LegalName = input.LegalName?.Trim();
        business.TaxId = input.TaxId?.Trim();
        business.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Business {BusinessId} updated.", id);

        return await GetAsync(id, ct);
    }

    public async Task<ManagedBusinessDto> SetPublicationAsync(Guid id, PublicationInputDto input, CancellationToken ct = default)
    {
        Business? business = await db.Businesses
            .Include(b => b.Branches.Where(br => br.DeletedAt == null && br.Status != "permanently_closed"))
                .ThenInclude(br => br.BranchCategories.Where(bc => bc.Active))
            .Include(b => b.Branches.Where(br => br.DeletedAt == null && br.Status != "permanently_closed"))
                .ThenInclude(br => br.BranchContacts.Where(c => c.Active && c.DeletedAt == null))
            .SingleOrDefaultAsync(b => b.Id == id, ct);

        if (business is null || business.DeletedAt != null || business.Status == "archived")
        {
            throw new BadHttpRequestException("El negocio no existe o está eliminado.", StatusCodes.Status404NotFound);
        }

        string targetStatus = input.Status.Trim().ToLowerInvariant();
        if (targetStatus != "draft" && targetStatus != "published")
        {
            throw new BadHttpRequestException("El estado de publicación solo puede ser 'draft' o 'published'.", StatusCodes.Status400BadRequest);
        }

        if (targetStatus == "published")
        {
            if (string.IsNullOrWhiteSpace(business.TradeName))
                throw new BadHttpRequestException("El negocio debe tener un nombre comercial no vacío.", StatusCodes.Status400BadRequest);
            if (string.IsNullOrWhiteSpace(business.Description))
                throw new BadHttpRequestException("El negocio debe tener una descripción no vacía para publicarse.", StatusCodes.Status400BadRequest);
            if (string.IsNullOrWhiteSpace(business.Slug))
                throw new BadHttpRequestException("El negocio debe tener un slug válido.", StatusCodes.Status400BadRequest);

            var activeBranches = business.Branches.ToList();
            if (activeBranches.Count == 0)
                throw new BadHttpRequestException("El negocio debe tener al menos una sucursal activa para publicarse.", StatusCodes.Status400BadRequest);

            foreach (var br in activeBranches)
            {
                if (string.IsNullOrWhiteSpace(br.Address))
                    throw new BadHttpRequestException($"La sucursal '{br.Name}' debe tener una dirección válida.", StatusCodes.Status400BadRequest);
                if (br.Location is null)
                    throw new BadHttpRequestException($"La sucursal '{br.Name}' debe tener una ubicación geográfica válida.", StatusCodes.Status400BadRequest);
                if (!br.TerritoryId.HasValue)
                    throw new BadHttpRequestException($"La sucursal '{br.Name}' debe pertenecer a un territorio válido.", StatusCodes.Status400BadRequest);
                if (!br.BranchCategories.Any(bc => bc.Active))
                    throw new BadHttpRequestException($"La sucursal '{br.Name}' debe tener al menos una categoría activa.", StatusCodes.Status400BadRequest);
                if (!br.BranchContacts.Any(c => c.Active && c.DeletedAt == null))
                    throw new BadHttpRequestException($"La sucursal '{br.Name}' debe tener al menos un contacto activo.", StatusCodes.Status400BadRequest);
            }
        }

        business.Status = targetStatus;
        business.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Business {BusinessId} publication set to '{Status}'.", id, targetStatus);

        return await GetAsync(id, ct);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        Business? business = await db.Businesses.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (business is null || business.DeletedAt != null || business.Status == "archived")
        {
            return; // Idempotente
        }

        business.Status = "archived";
        business.DeletedAt = DateTime.UtcNow;
        business.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Business {BusinessId} archived and soft-deleted.", id);
    }

    public async Task<ManagedBusinessDto> RestoreAsync(Guid id, RestoreInputDto input, CancellationToken ct = default)
    {
        Business? business = await db.Businesses
            .Include(b => b.Branches)
            .SingleOrDefaultAsync(b => b.Id == id, ct);

        if (business is null || (business.DeletedAt == null && business.Status != "archived"))
        {
            throw new BadHttpRequestException("Solo se pueden restaurar negocios archivados o eliminados.", StatusCodes.Status409Conflict);
        }

        var branchesToRestore = business.Branches
            .Where(br => input.BranchIds.Contains(br.Id) && br.DeletedAt != null)
            .ToList();

        if (branchesToRestore.Count == 0)
        {
            throw new BadHttpRequestException("Ninguna de las sucursales especificadas pertenece al negocio o está eliminada.", StatusCodes.Status400BadRequest);
        }

        DateTime now = DateTime.UtcNow;
        business.Status = "draft";
        business.DeletedAt = null;
        business.UpdatedAt = now;

        foreach (var br in branchesToRestore)
        {
            br.DeletedAt = null;
            br.UpdatedAt = now;
            br.Status = "active";
        }

        if (!business.Branches.Any(br => br.DeletedAt == null && br.IsPrimary))
        {
            var firstBranch = branchesToRestore.OrderBy(br => br.CreatedAt).ThenBy(br => br.Id).First();
            firstBranch.IsPrimary = true;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Business {BusinessId} restored as draft.", id);

        return await GetAsync(id, ct);
    }

    public async Task<(ManagedBusinessDto Business, Guid BranchId)> AddBranchAsync(
        Guid businessId, BranchInputDto input, CancellationToken ct = default)
    {
        Business? business = await db.Businesses
            .Include(b => b.Branches)
            .SingleOrDefaultAsync(b => b.Id == businessId, ct);

        if (business is null || business.DeletedAt != null || business.Status == "archived")
        {
            throw new BadHttpRequestException("El negocio no existe o está eliminado.", StatusCodes.Status409Conflict);
        }

        int activeBranches = business.Branches.Count(br => br.DeletedAt == null);
        if (activeBranches >= 100)
        {
            throw new BadHttpRequestException("El negocio alcanzó el límite de 100 sucursales activas.", StatusCodes.Status409Conflict);
        }
        if (business.Branches.Count >= 500)
        {
            throw new BadHttpRequestException("El negocio alcanzó el límite histórico de 500 sucursales.", StatusCodes.Status409Conflict);
        }

        DateTime now = DateTime.UtcNow;
        bool isPrimary = input.IsPrimary;
        if (input.IsPrimary)
        {
            foreach (var br in business.Branches.Where(br => br.DeletedAt == null && br.IsPrimary))
            {
                br.IsPrimary = false;
            }
        }
        else if (activeBranches == 0)
        {
            isPrimary = true;
        }

        var branchLocation = GeometryFactory.CreatePoint(
            new Coordinate(input.Location.Coordinates[0], input.Location.Coordinates[1]));

        var newBranch = new Branch
        {
            Id = Guid.CreateVersion7(),
            BusinessId = businessId,
            Name = input.Name.Trim(),
            Address = input.Address.Trim(),
            TerritoryId = input.TerritoryId,
            Location = branchLocation,
            Status = input.Status == "temporarily_closed" ? "temporarily_closed" : "active",
            IsPrimary = isPrimary,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Branches.Add(newBranch);
        AddBranchCollections(newBranch.Id, input, now);

        business.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Branch {BranchId} added to business {BusinessId}.", newBranch.Id, businessId);
        var updated = await GetAsync(businessId, ct);
        return (updated, newBranch.Id);
    }

    public async Task<ManagedBusinessDto> UpdateBranchAsync(
        Guid branchId, BranchInputDto input, CancellationToken ct = default)
    {
        Branch? branch = await db.Branches
            .Include(br => br.Business)
            .Include(br => br.BranchCategories)
            .Include(br => br.BranchContacts)
            .Include(br => br.OpeningHours)
            .Include(br => br.BranchHourOverrides)
            .SingleOrDefaultAsync(br => br.Id == branchId, ct);

        if (branch is null || branch.DeletedAt != null || branch.Business.DeletedAt != null || branch.Business.Status == "archived")
        {
            throw new BadHttpRequestException("La sucursal no existe o su negocio está eliminado.", StatusCodes.Status409Conflict);
        }

        Guid businessId = branch.BusinessId;
        DateTime now = DateTime.UtcNow;

        if (branch.IsPrimary && !input.IsPrimary)
        {
            bool hasOtherPrimary = await db.Branches.AnyAsync(br =>
                br.BusinessId == businessId && br.Id != branchId && br.DeletedAt == null && br.IsPrimary, ct);
            if (!hasOtherPrimary)
            {
                throw new BadHttpRequestException(
                    "No se puede desmarcar la única sucursal principal sin designar otra previamente.",
                    StatusCodes.Status409Conflict);
            }
        }
        else if (input.IsPrimary)
        {
            var otherPrimaries = await db.Branches
                .Where(br => br.BusinessId == businessId && br.Id != branchId && br.DeletedAt == null && br.IsPrimary)
                .ToListAsync(ct);
            foreach (var p in otherPrimaries) p.IsPrimary = false;
        }

        branch.Name = input.Name.Trim();
        branch.Address = input.Address.Trim();
        branch.TerritoryId = input.TerritoryId;
        branch.Location = GeometryFactory.CreatePoint(
            new Coordinate(input.Location.Coordinates[0], input.Location.Coordinates[1]));
        branch.Status = input.Status == "temporarily_closed" ? "temporarily_closed" : "active";
        branch.IsPrimary = input.IsPrimary;
        branch.UpdatedAt = now;

        ReplaceBranchCollections(branch, input, now);

        branch.Business.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Branch {BranchId} updated.", branchId);
        return await GetAsync(businessId, ct);
    }

    public async Task SoftDeleteBranchAsync(Guid branchId, CancellationToken ct = default)
    {
        Branch? branch = await db.Branches
            .Include(br => br.Business)
            .SingleOrDefaultAsync(br => br.Id == branchId, ct);

        if (branch is null || branch.DeletedAt != null)
        {
            return; // Idempotente
        }

        Guid businessId = branch.BusinessId;
        int activeCount = await db.Branches.CountAsync(br => br.BusinessId == businessId && br.DeletedAt == null, ct);
        if (activeCount <= 1)
        {
            throw new BadHttpRequestException("No se puede eliminar la única sucursal activa del negocio.", StatusCodes.Status409Conflict);
        }

        DateTime now = DateTime.UtcNow;
        branch.DeletedAt = now;
        branch.UpdatedAt = now;
        branch.Status = "permanently_closed";

        if (branch.IsPrimary)
        {
            branch.IsPrimary = false;
            var remainingPrimary = await db.Branches
                .Where(br => br.BusinessId == businessId && br.Id != branchId && br.DeletedAt == null)
                .OrderBy(br => br.CreatedAt)
                .ThenBy(br => br.Id)
                .FirstOrDefaultAsync(ct);

            if (remainingPrimary != null)
            {
                remainingPrimary.IsPrimary = true;
                remainingPrimary.UpdatedAt = now;
            }
        }

        branch.Business.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Branch {BranchId} soft-deleted.", branchId);
    }

    public async Task<ManagedBusinessDto> RestoreBranchAsync(Guid branchId, CancellationToken ct = default)
    {
        Branch? branch = await db.Branches
            .Include(br => br.Business)
            .SingleOrDefaultAsync(br => br.Id == branchId, ct);

        if (branch is null || branch.DeletedAt == null)
        {
            return branch != null ? await GetAsync(branch.BusinessId, ct) :
                throw new BadHttpRequestException("La sucursal no fue encontrada.", StatusCodes.Status404NotFound);
        }

        if (branch.Business.DeletedAt != null || branch.Business.Status == "archived")
        {
            throw new BadHttpRequestException("No se puede restaurar una sucursal de un negocio eliminado.", StatusCodes.Status409Conflict);
        }

        DateTime now = DateTime.UtcNow;
        branch.DeletedAt = null;
        branch.Status = "active";
        branch.UpdatedAt = now;

        bool hasPrimary = await db.Branches.AnyAsync(br =>
            br.BusinessId == branch.BusinessId && br.DeletedAt == null && br.IsPrimary, ct);
        if (!hasPrimary)
        {
            branch.IsPrimary = true;
        }

        branch.Business.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Branch {BranchId} restored.", branchId);
        return await GetAsync(branch.BusinessId, ct);
    }

    public async Task<(ManagedBusinessDto Business, Guid PhotoId)> AddPhotoAsync(
        Guid branchId, IFormFile file, PhotoInputDto meta, CancellationToken ct = default)
    {
        Branch? branch = await db.Branches
            .Include(br => br.Business)
            .Include(br => br.BranchPhotos)
            .SingleOrDefaultAsync(br => br.Id == branchId, ct);

        if (branch is null || branch.DeletedAt != null || branch.Business.DeletedAt != null || branch.Business.Status == "archived")
        {
            throw new BadHttpRequestException("La sucursal no existe o está eliminada.", StatusCodes.Status409Conflict);
        }

        int activePhotos = branch.BranchPhotos.Count(p => p.Active && p.DeletedAt == null);
        if (activePhotos >= 20)
        {
            throw new BadHttpRequestException("La sucursal alcanzó el límite de 20 fotos activas.", StatusCodes.Status409Conflict);
        }

        if (file.Length == 0 || file.Length > 5 * 1024 * 1024)
        {
            throw new BadHttpRequestException("El archivo debe tener entre 1 y 5242880 bytes.", StatusCodes.Status413PayloadTooLarge);
        }

        string contentType = file.ContentType.ToLowerInvariant();
        if (contentType != "image/jpeg" && contentType != "image/png" && contentType != "image/webp")
        {
            throw new BadHttpRequestException("Solo se permiten formatos JPEG, PNG y WebP.", StatusCodes.Status415UnsupportedMediaType);
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        byte[] bytes = ms.ToArray();

        DateTime now = DateTime.UtcNow;
        bool isPrimary = meta.IsPrimary;
        if (activePhotos == 0)
        {
            isPrimary = true;
        }
        else if (isPrimary)
        {
            foreach (var p in branch.BranchPhotos.Where(p => p.Active && p.DeletedAt == null && p.IsPrimary))
            {
                p.IsPrimary = false;
            }
        }

        var photo = new BranchPhoto
        {
            Id = Guid.CreateVersion7(),
            BranchId = branchId,
            PhotoData = bytes,
            MimeType = contentType,
            FileSize = bytes.Length,
            AltText = meta.AltText?.Trim(),
            IsPrimary = isPrimary,
            SortOrder = meta.SortOrder,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.BranchPhotos.Add(photo);
        branch.Business.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Photo {PhotoId} added to branch {BranchId}.", photo.Id, branchId);
        var updated = await GetAsync(branch.BusinessId, ct);
        return (updated, photo.Id);
    }

    public async Task<ManagedBusinessDto> UpdatePhotoMetaAsync(
        Guid photoId, PhotoInputDto input, CancellationToken ct = default)
    {
        BranchPhoto? photo = await db.BranchPhotos
            .Include(p => p.Branch)
                .ThenInclude(br => br.Business)
            .SingleOrDefaultAsync(p => p.Id == photoId, ct);

        if (photo is null || !photo.Active || photo.DeletedAt != null)
        {
            throw new BadHttpRequestException("La foto no fue encontrada o está eliminada.", StatusCodes.Status404NotFound);
        }

        Guid branchId = photo.BranchId;
        DateTime now = DateTime.UtcNow;

        if (photo.IsPrimary && !input.IsPrimary)
        {
            bool hasOtherPrimary = await db.BranchPhotos.AnyAsync(p =>
                p.BranchId == branchId && p.Id != photoId && p.Active && p.DeletedAt == null && p.IsPrimary, ct);
            if (!hasOtherPrimary)
            {
                throw new BadHttpRequestException(
                    "No se puede desmarcar la foto de portada sin designar otra previamente.",
                    StatusCodes.Status409Conflict);
            }
        }
        else if (input.IsPrimary)
        {
            var otherPrimaries = await db.BranchPhotos
                .Where(p => p.BranchId == branchId && p.Id != photoId && p.Active && p.DeletedAt == null && p.IsPrimary)
                .ToListAsync(ct);
            foreach (var p in otherPrimaries) p.IsPrimary = false;
        }

        photo.AltText = input.AltText?.Trim();
        photo.IsPrimary = input.IsPrimary;
        photo.SortOrder = input.SortOrder;
        photo.UpdatedAt = now;

        photo.Branch.Business.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Photo {PhotoId} updated.", photoId);
        return await GetAsync(photo.Branch.BusinessId, ct);
    }

    public async Task SoftDeletePhotoAsync(Guid photoId, CancellationToken ct = default)
    {
        BranchPhoto? photo = await db.BranchPhotos
            .Include(p => p.Branch)
                .ThenInclude(br => br.Business)
            .SingleOrDefaultAsync(p => p.Id == photoId, ct);

        if (photo is null || !photo.Active || photo.DeletedAt != null)
        {
            return; // Idempotente
        }

        DateTime now = DateTime.UtcNow;
        photo.Active = false;
        photo.DeletedAt = now;
        photo.UpdatedAt = now;

        if (photo.IsPrimary)
        {
            photo.IsPrimary = false;
            var nextPrimary = await db.BranchPhotos
                .Where(p => p.BranchId == photo.BranchId && p.Id != photoId && p.Active && p.DeletedAt == null)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Id)
                .FirstOrDefaultAsync(ct);

            if (nextPrimary != null)
            {
                nextPrimary.IsPrimary = true;
                nextPrimary.UpdatedAt = now;
            }
        }

        photo.Branch.Business.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Photo {PhotoId} soft-deleted.", photoId);
    }

    public async Task<(byte[] Data, string ContentType)> GetPhotoAsync(
        Guid photoId, bool isAdmin, CancellationToken ct = default)
    {
        var photo = await db.BranchPhotos.AsNoTracking()
            .Include(p => p.Branch)
                .ThenInclude(br => br.Business)
            .SingleOrDefaultAsync(p => p.Id == photoId, ct);

        if (photo is null || !photo.Active || photo.DeletedAt != null)
        {
            throw new BadHttpRequestException("La foto no fue encontrada.", StatusCodes.Status404NotFound);
        }

        if (!isAdmin)
        {
            if (photo.Branch.DeletedAt != null || photo.Branch.Status == "permanently_closed")
            {
                throw new BadHttpRequestException("La foto no está disponible.", StatusCodes.Status404NotFound);
            }
            if (photo.Branch.Business.DeletedAt != null || photo.Branch.Business.Status != "published")
            {
                throw new BadHttpRequestException("La foto no está disponible.", StatusCodes.Status404NotFound);
            }
        }

        return (photo.PhotoData, photo.MimeType);
    }

    private void AddBranchCollections(Guid branchId, BranchInputDto input, DateTime now)
    {
        foreach (Guid catId in input.CategoryIds.Distinct())
        {
            db.BranchCategories.Add(new BranchCategory
            {
                BranchId = branchId,
                CategoryId = catId,
                Active = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        foreach (var c in input.Contacts)
        {
            db.BranchContacts.Add(new BranchContact
            {
                Id = Guid.CreateVersion7(),
                BranchId = branchId,
                ContactType = c.Type.Trim().ToLowerInvariant(),
                ContactValue = c.Value.Trim(),
                IsPrimary = false,
                Active = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        foreach (var oh in input.OpeningHours)
        {
            db.OpeningHours.Add(new OpeningHour
            {
                Id = Guid.CreateVersion7(),
                BranchId = branchId,
                Weekday = (short)oh.Weekday,
                OpensAt = oh.OpensAt,
                ClosesAt = oh.ClosesAt,
                Active = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        foreach (var ho in input.HourOverrides)
        {
            db.BranchHourOverrides.Add(new BranchHourOverride
            {
                Id = Guid.CreateVersion7(),
                BranchId = branchId,
                ServiceDate = ho.Date,
                IsClosed = ho.IsClosed,
                OpensAt = ho.OpensAt,
                ClosesAt = ho.ClosesAt,
                Reason = ho.Reason?.Trim(),
                Active = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private void ReplaceBranchCollections(Branch branch, BranchInputDto input, DateTime now)
    {
        // Categorías: desactivar ausentes, activar/añadir presentes
        var existingCats = branch.BranchCategories.ToList();
        var targetCatIds = input.CategoryIds.Distinct().ToHashSet();

        foreach (var bc in existingCats)
        {
            if (!targetCatIds.Contains(bc.CategoryId))
            {
                bc.Active = false;
                bc.UpdatedAt = now;
            }
            else
            {
                bc.Active = true;
                bc.UpdatedAt = now;
                targetCatIds.Remove(bc.CategoryId);
            }
        }
        foreach (var newCatId in targetCatIds)
        {
            branch.BranchCategories.Add(new BranchCategory
            {
                BranchId = branch.Id,
                CategoryId = newCatId,
                Active = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // Contactos: desactivar anteriores, agregar nuevos
        foreach (var c in branch.BranchContacts.Where(c => c.Active && c.DeletedAt == null))
        {
            c.Active = false;
            c.DeletedAt = now;
            c.UpdatedAt = now;
        }
        foreach (var c in input.Contacts)
        {
            branch.BranchContacts.Add(new BranchContact
            {
                Id = Guid.CreateVersion7(),
                BranchId = branch.Id,
                ContactType = c.Type.Trim().ToLowerInvariant(),
                ContactValue = c.Value.Trim(),
                Active = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // Horarios: desactivar anteriores, agregar nuevos
        foreach (var oh in branch.OpeningHours.Where(oh => oh.Active))
        {
            oh.Active = false;
            oh.UpdatedAt = now;
        }
        foreach (var oh in input.OpeningHours)
        {
            branch.OpeningHours.Add(new OpeningHour
            {
                Id = Guid.CreateVersion7(),
                BranchId = branch.Id,
                Weekday = (short)oh.Weekday,
                OpensAt = oh.OpensAt,
                ClosesAt = oh.ClosesAt,
                Active = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // Overrides
        foreach (var ho in branch.BranchHourOverrides.Where(ho => ho.Active && ho.DeletedAt == null))
        {
            ho.Active = false;
            ho.UpdatedAt = now;
        }
        foreach (var ho in input.HourOverrides)
        {
            branch.BranchHourOverrides.Add(new BranchHourOverride
            {
                Id = Guid.CreateVersion7(),
                BranchId = branch.Id,
                ServiceDate = ho.Date,
                IsClosed = ho.IsClosed,
                OpensAt = ho.OpensAt,
                ClosesAt = ho.ClosesAt,
                Reason = ho.Reason?.Trim(),
                Active = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private static ManagedBusinessDto MapToManagedBusinessDto(Business business)
    {
        var branches = business.Branches
            .OrderBy(br => br.DeletedAt != null)
            .ThenByDescending(br => br.IsPrimary)
            .ThenBy(br => br.Name)
            .ThenBy(br => br.Id)
            .Select(br =>
            {
                var input = new BranchInputDto
                {
                    Name = br.Name,
                    Address = br.Address,
                    TerritoryId = br.TerritoryId ?? Guid.Empty,
                    Location = PointDto.FromPoint(br.Location),
                    Status = br.Status,
                    IsPrimary = br.IsPrimary,
                    CategoryIds = br.BranchCategories.Where(bc => bc.Active).Select(bc => bc.CategoryId).ToList(),
                    Contacts = br.BranchContacts.Where(c => c.Active && c.DeletedAt == null).Select(c => new ContactInputDto
                    {
                        Type = c.ContactType,
                        Value = c.ContactValue
                    }).ToList(),
                    HoursKnown = br.OpeningHours.Any(oh => oh.Active),
                    OpeningHours = br.OpeningHours.Where(oh => oh.Active).OrderBy(oh => oh.Weekday).ThenBy(oh => oh.OpensAt)
                        .Select(oh => new OpeningHourInputDto
                        {
                            Weekday = oh.Weekday,
                            OpensAt = oh.OpensAt,
                            ClosesAt = oh.ClosesAt
                        }).ToList(),
                    HourOverrides = br.BranchHourOverrides.Where(ho => ho.Active && ho.DeletedAt == null).OrderBy(ho => ho.ServiceDate)
                        .Select(ho => new HourOverrideInputDto
                        {
                            Date = ho.ServiceDate,
                            IsClosed = ho.IsClosed,
                            OpensAt = ho.OpensAt,
                            ClosesAt = ho.ClosesAt,
                            Reason = ho.Reason
                        }).ToList()
                };

                var photos = br.BranchPhotos.Where(p => p.Active && p.DeletedAt == null)
                    .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                    .Select(p => new PhotoDto(p.Id, $"/api/v1/photos/{p.Id}", p.AltText, p.IsPrimary, p.SortOrder))
                    .ToList();

                return new ManagedBranchDto(
                    br.Id,
                    br.BusinessId,
                    new DateTimeOffset(br.CreatedAt, TimeSpan.Zero),
                    br.DeletedAt.HasValue ? new DateTimeOffset(br.DeletedAt.Value, TimeSpan.Zero) : null,
                    input,
                    photos
                );
            }).ToList();

        var privateData = new PrivateDataDto(business.LegalName, business.TaxId);

        return new ManagedBusinessDto(
            business.Id,
            business.Slug ?? string.Empty,
            business.TradeName,
            business.Description,
            business.Status,
            null,
            business.Status,
            new DateTimeOffset(business.CreatedAt, TimeSpan.Zero),
            new DateTimeOffset(business.UpdatedAt ?? business.CreatedAt, TimeSpan.Zero),
            business.DeletedAt.HasValue ? new DateTimeOffset(business.DeletedAt.Value, TimeSpan.Zero) : null,
            branches,
            privateData
        );
    }

    private static string GenerateSlugBase(string tradeName)
    {
        string normalized = tradeName.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (c is ' ' or '-' or '_')
                {
                    if (sb.Length > 0 && sb[^1] != '-')
                    {
                        sb.Append('-');
                    }
                }
            }
        }
        string result = sb.ToString().Trim('-');
        if (string.IsNullOrEmpty(result)) result = "negocio";
        if (result.Length > 140) result = result[..140].TrimEnd('-');
        return result;
    }
}
