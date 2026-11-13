namespace backend.DTOs.Categories;

/// <summary>Reemplaza los datos editables; ParentId nulo elimina la relación con el padre.</summary>
public sealed class UpdateCategoryRequest : CreateCategoryRequest;
