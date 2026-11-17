using backend.Data;
using backend.DTOs;
using backend.DTOs.Branches;
using backend.DTOs.Businesses;
using backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Services;

public sealed class BranchSearchService(AppDbContext db, ILogger<BranchSearchService> logger) : IBranchSearchService
{
    private static readonly GeometryFactory GeometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(4326);

    public async Task<PagedResult<BranchSummaryDto>> SearchAsync(
        BranchSearchRequest request, DateTimeOffset evaluatedAt, CancellationToken ct = default)
    {
        logger.LogDebug("Searching branches: Q={Q}, Page={Page}, PageSize={PageSize}", request.Q, request.Page, request.PageSize);
        ValidateSearchRequest(request);

        long offset = ((long)request.Page - 1) * request.PageSize;

        Point? center = null;
        int? radiusM = null;
        if (request.Lat.HasValue && request.Lng.HasValue)
        {
            center = GeometryFactory.CreatePoint(new Coordinate(request.Lng.Value, request.Lat.Value));
            radiusM = request.RadiusM ?? 10000;
        }

        Geometry? bboxGeom = ParseBbox(request.Bbox);

        HashSet<Guid>? categoryIds = request.CategoryId.HasValue
            ? await GetCategoryDescendantIdsAsync(request.CategoryId.Value, ct)
            : null;

        HashSet<Guid>? territoryIds = request.TerritoryId.HasValue
            ? await GetTerritoryDescendantIdsAsync(request.TerritoryId.Value, ct)
            : null;

        IQueryable<PublishedBranch> baseQuery = db.PublishedBranches.AsNoTracking();

        string? q = request.Q?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            string pattern = "%" + q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            baseQuery = baseQuery.Where(pb =>
                EF.Functions.ILike(pb.TradeName!, pattern, "\\") ||
                EF.Functions.ILike(pb.BranchName!, pattern, "\\"));
        }

        if (categoryIds is not null)
        {
            baseQuery = baseQuery.Where(pb => db.BranchCategories.Any(bc =>
                bc.BranchId == pb.BranchId && bc.Active && categoryIds.Contains(bc.CategoryId)));
        }

        if (territoryIds is not null)
        {
            baseQuery = baseQuery.Where(pb => pb.TerritoryId.HasValue && territoryIds.Contains(pb.TerritoryId.Value));
        }

        if (center is not null && radiusM.HasValue)
        {
            baseQuery = baseQuery.Where(pb => pb.Location != null && pb.Location.IsWithinDistance(center, radiusM.Value));
        }
        else if (bboxGeom is not null)
        {
            baseQuery = baseQuery.Where(pb => pb.Location != null && bboxGeom.Covers(pb.Location));
        }

        if (request.OpenNow)
        {
            DateTime utcCheck = evaluatedAt.UtcDateTime;
            baseQuery = baseQuery.Where(pb => pb.BranchId.HasValue && AppDbContext.IsOpenAt(pb.BranchId.Value, utcCheck));
        }

        int total = await baseQuery.CountAsync(ct);
        int totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / request.PageSize);

        if (offset >= total)
        {
            return new PagedResult<BranchSummaryDto>([], request.Page, request.PageSize, total);
        }

        var joinedQuery = from pb in baseQuery
                          join b in db.Businesses.AsNoTracking() on pb.BusinessId equals b.Id
                          select new
                          {
                              pb.BranchId,
                              pb.BusinessId,
                              BusinessSlug = b.Slug ?? string.Empty,
                              TradeName = pb.TradeName ?? string.Empty,
                              BranchName = pb.BranchName ?? string.Empty,
                              Address = pb.Address ?? string.Empty,
                              TerritoryId = pb.TerritoryId ?? Guid.Empty,
                              Location = pb.Location!,
                              pb.BranchStatus,
                              pb.BusinessStatus,
                              Distance = center != null ? (double?)pb.Location!.Distance(center) : null
                          };

        var orderedQuery = center != null
            ? joinedQuery.OrderBy(x => x.Distance).ThenBy(x => x.TradeName).ThenBy(x => x.BranchId)
            : joinedQuery.OrderBy(x => x.TradeName).ThenBy(x => x.BranchName).ThenBy(x => x.BranchId);

        var pagedRows = await orderedQuery
            .Skip((int)offset)
            .Take(request.PageSize)
            .ToListAsync(ct);

        List<Guid> branchIds = pagedRows
            .Where(r => r.BranchId.HasValue)
            .Select(r => r.BranchId!.Value)
            .ToList();

        var coverPhotos = await db.BranchPhotos.AsNoTracking()
            .Where(p => branchIds.Contains(p.BranchId) && p.Active && p.DeletedAt == null && p.IsPrimary)
            .Select(p => new { p.BranchId, p.Id })
            .ToDictionaryAsync(p => p.BranchId, p => $"/api/v1/photos/{p.Id}", ct);

        DateTime checkTime = evaluatedAt.UtcDateTime;
        var items = new List<BranchSummaryDto>(pagedRows.Count);

        foreach (var row in pagedRows)
        {
            if (!row.BranchId.HasValue || !row.BusinessId.HasValue) continue;

            Guid bId = row.BranchId.Value;
            bool isTempClosed = row.BranchStatus == "temporarily_closed" || row.BusinessStatus == "temporarily_closed";
            string effectiveStatus = isTempClosed ? "temporarily_closed" : "active";

            bool? isOpen = isTempClosed ? false : (bool?)null;
            int? distanceInt = row.Distance.HasValue ? (int)Math.Round(row.Distance.Value) : null;
            coverPhotos.TryGetValue(bId, out string? coverPhotoUrl);

            items.Add(new BranchSummaryDto(
                bId,
                row.BusinessId.Value,
                row.BusinessSlug,
                row.TradeName,
                row.BranchName,
                row.Address,
                row.TerritoryId,
                PointDto.FromPoint(row.Location),
                distanceInt,
                effectiveStatus,
                isOpen,
                null,
                coverPhotoUrl
            ));
        }

        return new PagedResult<BranchSummaryDto>(items, request.Page, request.PageSize, total);
    }

    public async Task<GeoJsonFeatureCollectionDto> SearchGeoJsonAsync(
        BranchSearchRequest request, DateTimeOffset evaluatedAt, CancellationToken ct = default)
    {
        PagedResult<BranchSummaryDto> paged = await SearchAsync(request, evaluatedAt, ct);

        int totalPages = paged.TotalCount == 0 ? 0 : (int)Math.Ceiling((double)paged.TotalCount / paged.PageSize);
        var pagination = new GeoJsonPaginationDto
        {
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalItems = paged.TotalCount,
            TotalPages = totalPages
        };

        var features = paged.Items.Select(item => new GeoJsonFeatureDto
        {
            Id = item.Id,
            Geometry = item.Location,
            Properties = new GeoJsonPropertiesDto
            {
                BusinessId = item.BusinessId,
                BusinessSlug = item.BusinessSlug,
                TradeName = item.TradeName,
                Name = item.Name,
                Address = item.Address,
                TerritoryId = item.TerritoryId,
                DistanceM = item.DistanceM,
                Status = item.Status,
                IsOpen = item.IsOpen,
                PriceLevel = item.PriceLevel,
                CoverPhotoUrl = item.CoverPhotoUrl
            }
        }).ToList();

        return new GeoJsonFeatureCollectionDto
        {
            Features = features,
            Pagination = pagination
        };
    }

    public async Task<PublicBusinessDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        string normalizedSlug = slug.Trim().ToLowerInvariant();

        Business? business = await db.Businesses.AsNoTracking()
            .Include(b => b.Branches.Where(br => br.DeletedAt == null && br.Status != "permanently_closed"))
                .ThenInclude(br => br.BranchCategories.Where(bc => bc.Active))
                    .ThenInclude(bc => bc.Category)
            .Include(b => b.Branches.Where(br => br.DeletedAt == null && br.Status != "permanently_closed"))
                .ThenInclude(br => br.BranchContacts.Where(c => c.Active && c.DeletedAt == null))
            .Include(b => b.Branches.Where(br => br.DeletedAt == null && br.Status != "permanently_closed"))
                .ThenInclude(br => br.OpeningHours.Where(oh => oh.Active))
            .Include(b => b.Branches.Where(br => br.DeletedAt == null && br.Status != "permanently_closed"))
                .ThenInclude(br => br.BranchHourOverrides.Where(ho => ho.Active && ho.DeletedAt == null))
            .Include(b => b.Branches.Where(br => br.DeletedAt == null && br.Status != "permanently_closed"))
                .ThenInclude(br => br.BranchPhotos.Where(p => p.Active && p.DeletedAt == null))
            .Include(b => b.Branches.Where(br => br.DeletedAt == null && br.Status != "permanently_closed"))
                .ThenInclude(br => br.BranchFeatures.Where(bf => bf.Active))
                    .ThenInclude(bf => bf.Feature)
            .Include(b => b.Branches.Where(br => br.DeletedAt == null && br.Status != "permanently_closed"))
                .ThenInclude(br => br.BranchPayments.Where(bp => bp.Active))
                    .ThenInclude(bp => bp.Method)
            .Include(b => b.Branches.Where(br => br.DeletedAt == null && br.Status != "permanently_closed"))
                .ThenInclude(br => br.BranchServiceProducts.Where(bsp => bsp.Active))
                    .ThenInclude(bsp => bsp.ServiceProduct)
            .Where(b => b.Slug == normalizedSlug && b.Status == "published" && b.DeletedAt == null)
            .SingleOrDefaultAsync(ct);

        if (business is null)
        {
            throw new BadHttpRequestException("El negocio no fue encontrado o no está publicado.", StatusCodes.Status404NotFound);
        }

        var visibleBranches = business.Branches
            .OrderByDescending(br => br.IsPrimary)
            .ThenBy(br => br.Name)
            .ThenBy(br => br.Id)
            .Take(100)
            .ToList();

        if (visibleBranches.Count == 0)
        {
            throw new BadHttpRequestException("El negocio no tiene sucursales disponibles.", StatusCodes.Status404NotFound);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var publicBranches = new List<PublicBranchDto>(visibleBranches.Count);

        foreach (Branch branch in visibleBranches)
        {
            publicBranches.Add(await MapToPublicBranchDtoAsync(business, branch, now, ct));
        }

        return new PublicBusinessDto(
            business.Id,
            business.Slug ?? string.Empty,
            business.TradeName,
            business.Description,
            business.Status,
            null,
            publicBranches
        );
    }

    public async Task<PublicBranchDto> GetBranchAsync(Guid id, CancellationToken ct = default)
    {
        Branch? branch = await db.Branches.AsNoTracking()
            .Include(br => br.Business)
            .Include(br => br.BranchCategories.Where(bc => bc.Active))
                .ThenInclude(bc => bc.Category)
            .Include(br => br.BranchContacts.Where(c => c.Active && c.DeletedAt == null))
            .Include(br => br.OpeningHours.Where(oh => oh.Active))
            .Include(br => br.BranchHourOverrides.Where(ho => ho.Active && ho.DeletedAt == null))
            .Include(br => br.BranchPhotos.Where(p => p.Active && p.DeletedAt == null))
            .Include(br => br.BranchFeatures.Where(bf => bf.Active))
                .ThenInclude(bf => bf.Feature)
            .Include(br => br.BranchPayments.Where(bp => bp.Active))
                .ThenInclude(bp => bp.Method)
            .Include(br => br.BranchServiceProducts.Where(bsp => bsp.Active))
                .ThenInclude(bsp => bsp.ServiceProduct)
            .Where(br => br.Id == id && br.DeletedAt == null && br.Status != "permanently_closed")
            .SingleOrDefaultAsync(ct);

        if (branch is null || branch.Business.Status != "published" || branch.Business.DeletedAt != null)
        {
            throw new BadHttpRequestException("La sucursal no fue encontrada o no está disponible.", StatusCodes.Status404NotFound);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        return await MapToPublicBranchDtoAsync(branch.Business, branch, now, ct);
    }

    private async Task<PublicBranchDto> MapToPublicBranchDtoAsync(
        Business business, Branch branch, DateTimeOffset evaluatedAt, CancellationToken ct)
    {
        bool isTempClosed = branch.Status == "temporarily_closed" || business.Status == "temporarily_closed";
        string effectiveStatus = isTempClosed ? "temporarily_closed" : "active";

        bool hasHours = branch.OpeningHours.Any(oh => oh.Active);
        bool? isOpen = isTempClosed ? false : (hasHours ? null : null);

        var territoryPath = branch.TerritoryId.HasValue
            ? await GetTerritoryPathAsync(branch.TerritoryId.Value, ct)
            : [];

        var categories = branch.BranchCategories
            .Where(bc => bc.Category != null && bc.Category.Active)
            .OrderBy(bc => bc.Category.Name)
            .ThenBy(bc => bc.Category.Id)
            .Select(bc => new CatalogRefDto(bc.Category.Id, bc.Category.Code, bc.Category.Name))
            .ToList();

        var services = branch.BranchServiceProducts
            .Where(bsp => bsp.ServiceProduct != null && bsp.ServiceProduct.Active)
            .OrderBy(bsp => bsp.ServiceProduct.Name)
            .ThenBy(bsp => bsp.ServiceProduct.Id)
            .Select(bsp => new CatalogRefDto(bsp.ServiceProduct.Id, bsp.ServiceProduct.Code, bsp.ServiceProduct.Name))
            .ToList();

        var features = branch.BranchFeatures
            .Where(bf => bf.Feature != null && bf.Feature.Active)
            .OrderBy(bf => bf.Feature.Name)
            .ThenBy(bf => bf.Feature.Id)
            .Select(bf => new CatalogRefDto(bf.Feature.Id, bf.Feature.Code, bf.Feature.Name))
            .ToList();

        var paymentMethods = branch.BranchPayments
            .Where(bp => bp.Method != null && bp.Method.Active)
            .OrderBy(bp => bp.Method.Name)
            .ThenBy(bp => bp.Method.Id)
            .Select(bp => new CatalogRefDto(bp.Method.Id, bp.Method.Code, bp.Method.Name))
            .ToList();

        var contacts = branch.BranchContacts
            .Where(c => c.Active && c.DeletedAt == null)
            .OrderBy(c => ContactSortOrder(c.ContactType))
            .Select(c => new ContactDto(c.ContactType, c.ContactValue))
            .ToList();

        var openingHours = branch.OpeningHours
            .Where(oh => oh.Active)
            .OrderBy(oh => oh.Weekday)
            .ThenBy(oh => oh.OpensAt)
            .Select(oh => new OpeningHourDto(oh.Weekday, oh.OpensAt, oh.ClosesAt))
            .ToList();

        DateOnly today = DateOnly.FromDateTime(evaluatedAt.Date);
        DateOnly maxDate = today.AddDays(365);
        var overrides = branch.BranchHourOverrides
            .Where(ho => ho.Active && ho.DeletedAt == null && ho.ServiceDate >= today.AddDays(-1) && ho.ServiceDate <= maxDate)
            .OrderBy(ho => ho.ServiceDate)
            .Select(ho => new HourOverrideDto(ho.ServiceDate, ho.IsClosed, ho.OpensAt, ho.ClosesAt, ho.Reason))
            .ToList();

        var photos = branch.BranchPhotos
            .Where(p => p.Active && p.DeletedAt == null)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .Select(p => new PhotoDto(p.Id, $"/api/v1/photos/{p.Id}", p.AltText, p.IsPrimary, p.SortOrder))
            .ToList();

        string? coverPhotoUrl = photos.FirstOrDefault(p => p.IsPrimary)?.Url ?? photos.FirstOrDefault()?.Url;

        var holidays = branch.TerritoryId.HasValue
            ? await GetHolidaysAsync(branch.TerritoryId.Value, today, maxDate, ct)
            : [];

        return new PublicBranchDto(
            branch.Id,
            business.Id,
            business.Slug ?? string.Empty,
            business.TradeName,
            branch.Name,
            branch.Address,
            branch.TerritoryId ?? Guid.Empty,
            PointDto.FromPoint(branch.Location),
            null,
            effectiveStatus,
            isOpen,
            null,
            coverPhotoUrl,
            business.Description,
            branch.IsPrimary,
            business.Status,
            branch.Status,
            territoryPath,
            categories,
            services,
            features,
            paymentMethods,
            contacts,
            hasHours,
            openingHours,
            overrides,
            holidays,
            "America/La_Paz",
            evaluatedAt,
            null,
            null,
            photos
        );
    }

    private static int ContactSortOrder(string type) => type switch
    {
        "phone" => 1,
        "whatsapp" => 2,
        "email" => 3,
        "website" => 4,
        "facebook" => 5,
        "instagram" => 6,
        _ => 99
    };

    private async Task<IReadOnlyList<TerritoryRefDto>> GetTerritoryPathAsync(Guid territoryId, CancellationToken ct)
    {
        var allTerritories = await db.Territories.AsNoTracking()
            .Where(t => t.Active && t.DeletedAt == null)
            .Select(t => new { t.Id, t.ParentId, t.TerritoryType, t.Name })
            .ToListAsync(ct);

        var map = allTerritories.ToDictionary(t => t.Id);
        var path = new List<TerritoryRefDto>();

        Guid? current = territoryId;
        var visited = new HashSet<Guid>();

        while (current.HasValue && visited.Add(current.Value) && map.TryGetValue(current.Value, out var item))
        {
            path.Add(new TerritoryRefDto(item.Id, item.TerritoryType, item.Name));
            current = item.ParentId;
        }

        path.Reverse();
        return path;
    }

    private async Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(
        Guid territoryId, DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var territories = await GetTerritoryPathAsync(territoryId, ct);
        var territoryIds = territories.Select(t => (Guid?)t.Id).Append(null).ToList();

        var holidays = await db.Holidays.AsNoTracking()
            .Where(h => h.Active && h.DeletedAt == null &&
                        territoryIds.Contains(h.TerritoryId) &&
                        h.HolidayDate >= fromDate && h.HolidayDate <= toDate)
            .OrderBy(h => h.HolidayDate)
            .ThenBy(h => h.Name)
            .Select(h => new HolidayDto(h.HolidayDate, h.Name))
            .ToListAsync(ct);

        return holidays;
    }

    private async Task<HashSet<Guid>> GetCategoryDescendantIdsAsync(Guid categoryId, CancellationToken ct)
    {
        var allCats = await db.Categories.AsNoTracking()
            .Where(c => c.Active && c.DeletedAt == null)
            .Select(c => new { c.Id, c.ParentId })
            .ToListAsync(ct);

        var result = new HashSet<Guid> { categoryId };
        bool added;
        do
        {
            added = false;
            foreach (var cat in allCats)
            {
                if (cat.ParentId.HasValue && result.Contains(cat.ParentId.Value) && result.Add(cat.Id))
                {
                    added = true;
                }
            }
        } while (added);

        return result;
    }

    private async Task<HashSet<Guid>> GetTerritoryDescendantIdsAsync(Guid territoryId, CancellationToken ct)
    {
        var allTerritories = await db.Territories.AsNoTracking()
            .Where(t => t.Active && t.DeletedAt == null)
            .Select(t => new { t.Id, t.ParentId })
            .ToListAsync(ct);

        var result = new HashSet<Guid> { territoryId };
        bool added;
        do
        {
            added = false;
            foreach (var t in allTerritories)
            {
                if (t.ParentId.HasValue && result.Contains(t.ParentId.Value) && result.Add(t.Id))
                {
                    added = true;
                }
            }
        } while (added);

        return result;
    }

    private static void ValidateSearchRequest(BranchSearchRequest request)
    {
        if (request.Lat.HasValue != request.Lng.HasValue)
        {
            throw new BadHttpRequestException(
                "Los parámetros 'lat' y 'lng' deben proporcionarse juntos.",
                StatusCodes.Status400BadRequest);
        }

        if (request.RadiusM.HasValue && !request.Lat.HasValue)
        {
            throw new BadHttpRequestException(
                "El parámetro 'radiusM' requiere que se especifique un centro con 'lat' y 'lng'.",
                StatusCodes.Status400BadRequest);
        }

        bool hasCenter = request.Lat.HasValue && request.Lng.HasValue;
        bool hasBbox = !string.IsNullOrWhiteSpace(request.Bbox);

        if (hasCenter && hasBbox)
        {
            throw new BadHttpRequestException(
                "Los filtros por centro/radio y bbox son mutuamente excluyentes.",
                StatusCodes.Status400BadRequest);
        }

        long offset = ((long)request.Page - 1) * request.PageSize;
        if (offset > 2000)
        {
            throw new BadHttpRequestException(
                "El desplazamiento de paginación excede el límite máximo permitido de 2000.",
                StatusCodes.Status400BadRequest);
        }
    }

    private static Geometry? ParseBbox(string? bbox)
    {
        if (string.IsNullOrWhiteSpace(bbox)) return null;

        string[] parts = bbox.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double minLng) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double minLat) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double maxLng) ||
            !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double maxLat))
        {
            throw new BadHttpRequestException(
                "El parámetro 'bbox' debe tener el formato 'minLng,minLat,maxLng,maxLat'.",
                StatusCodes.Status400BadRequest);
        }

        if (minLng < -180 || maxLng > 180 || minLat < -90 || maxLat > 90 || minLng >= maxLng || minLat >= maxLat)
        {
            throw new BadHttpRequestException(
                "Los límites del parámetro 'bbox' son inválidos o están fuera de rango.",
                StatusCodes.Status400BadRequest);
        }

        return GeometryFactory.ToGeometry(new Envelope(minLng, maxLng, minLat, maxLat));
    }
}
