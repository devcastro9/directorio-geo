using System;
using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Branches;

/// <summary>Parámetros de consulta para búsqueda de sucursales.</summary>
public sealed class BranchSearchRequest
{
    public string? Q { get; init; }

    public Guid? CategoryId { get; init; }

    public Guid? TerritoryId { get; init; }

    [Range(-90.0, 90.0)]
    public double? Lat { get; init; }

    [Range(-180.0, 180.0)]
    public double? Lng { get; init; }

    [Range(1, 50000)]
    public int? RadiusM { get; init; }

    public string? Bbox { get; init; }

    public bool OpenNow { get; init; } = false;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public string Format { get; init; } = "json";
}
