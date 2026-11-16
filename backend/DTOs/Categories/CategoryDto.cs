using System;

namespace backend.DTOs.Categories;

/// <summary>
/// Datos públicos de una categoría del catálogo.
/// La jerarquía se transmite de forma plana; el cliente construye el árbol usando <see cref="ParentId"/>.
/// </summary>
public sealed record CategoryDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    Guid? ParentId,
    int SortOrder,
    bool IsActive);
