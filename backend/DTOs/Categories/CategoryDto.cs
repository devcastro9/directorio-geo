using System;

namespace backend.DTOs.Categories;

/// <summary>Datos públicos de una categoría.</summary>
public sealed record CategoryDto(
    Guid Id, string Code, string Name, string? Description,
    Guid? ParentId, int SortOrder, bool Active,
    DateTime CreatedAt, DateTime UpdatedAt);
