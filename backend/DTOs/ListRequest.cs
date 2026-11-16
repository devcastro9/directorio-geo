using System;
using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>Filtro y paginación del catálogo activo.</summary>
public sealed class ListRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    /// <summary>Búsqueda textual en nombre y código (substring, insensible a mayúsculas).</summary>
    public string? Search { get; init; }

    /// <summary>
    /// UUID de la categoría padre cuyos hijos directos se quieren obtener.
    /// Mutuamente excluyente con <see cref="RootOnly"/>.
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>
    /// <c>true</c> para devolver solo categorías raíz (sin padre).
    /// Mutuamente excluyente con <see cref="ParentId"/>.
    /// </summary>
    public bool RootOnly { get; init; }
}
