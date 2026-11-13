using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>Filtro y paginación del catálogo activo.</summary>
public sealed class ListRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public string? Search { get; init; }
}
