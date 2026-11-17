using backend.DTOs;
using backend.DTOs.Branches;
using backend.DTOs.Businesses;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Services;

public interface IBranchSearchService
{
    Task<PagedResult<BranchSummaryDto>> SearchAsync(BranchSearchRequest request, DateTimeOffset evaluatedAt, CancellationToken ct = default);
    Task<GeoJsonFeatureCollectionDto> SearchGeoJsonAsync(BranchSearchRequest request, DateTimeOffset evaluatedAt, CancellationToken ct = default);
    Task<PublicBusinessDto> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<PublicBranchDto> GetBranchAsync(Guid id, CancellationToken ct = default);
}
