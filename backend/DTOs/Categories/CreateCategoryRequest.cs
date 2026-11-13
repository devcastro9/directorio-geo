using System;
using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Categories;

/// <summary>Datos necesarios para crear una categoría activa.</summary>
public class CreateCategoryRequest
{
    [Required]
    [StringLength(50)]
    public string Code { get; init; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }
    public Guid? ParentId { get; init; }
    public int SortOrder { get; init; }
}
