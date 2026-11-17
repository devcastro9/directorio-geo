using System;
using System.Collections.Generic;

namespace backend.DTOs;

public sealed class GeoJsonPaginationDto
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
}

public sealed class GeoJsonPropertiesDto
{
    public Guid BusinessId { get; init; }
    public string BusinessSlug { get; init; } = null!;
    public string TradeName { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Address { get; init; } = null!;
    public Guid TerritoryId { get; init; }
    public int? DistanceM { get; init; }
    public string Status { get; init; } = null!;
    public bool? IsOpen { get; init; }
    public int? PriceLevel { get; init; }
    public string? CoverPhotoUrl { get; init; }
}

public sealed class GeoJsonFeatureDto
{
    public string Type { get; } = "Feature";
    public Guid Id { get; init; }
    public PointDto Geometry { get; init; } = null!;
    public GeoJsonPropertiesDto Properties { get; init; } = null!;
}

public sealed class GeoJsonFeatureCollectionDto
{
    public string Type { get; } = "FeatureCollection";
    public IReadOnlyList<GeoJsonFeatureDto> Features { get; init; } = [];
    public GeoJsonPaginationDto Pagination { get; init; } = null!;
}
