using System;
using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Categories;

/// <summary>
/// Reemplaza los campos editables de una categoría existente.
/// A diferencia de <see cref="CreateCategoryRequest"/>, <c>code</c> es editable en PUT
/// y se agrega <c>isActive</c> para activar/desactivar sin borrar la categoría.
/// </summary>
public sealed class UpdateCategoryRequest
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Code { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public Guid? ParentId { get; init; }

    public int SortOrder { get; init; }

    public bool IsActive { get; init; } = true;
}
