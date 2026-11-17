using System;

namespace backend.DTOs.Branches;

public sealed record BranchSummaryDto(
    Guid Id,
    Guid BusinessId,
    string BusinessSlug,
    string TradeName,
    string Name,
    string Address,
    Guid TerritoryId,
    PointDto Location,
    int? DistanceM,
    string Status,
    bool? IsOpen,
    int? PriceLevel,
    string? CoverPhotoUrl);
