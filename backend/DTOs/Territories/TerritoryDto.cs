using System;
using System.Collections.Generic;

namespace backend.DTOs.Territories;

public sealed record TerritoryDto(
    Guid Id,
    Guid? ParentId,
    string Type,
    string Name,
    string? CodeIne,
    double[]? Bounds
);

public sealed class TerritoryListRequest
{
    public string? Type { get; init; }   // department|province|municipality|zone|neighborhood
    public Guid? ParentId { get; init; } // hijos directos

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
