using backend.DTOs;
using backend.DTOs.Categories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Services;

/// <summary>Gestiona categorías del catálogo sin modificar los modelos Database First.</summary>
public interface ICategoryService
{
    /// <summary>Lista categorías activas por nombre o código, en orden estable.</summary>
    Task<PagedResult<CategoryDto>> ListAsync(ListRequest request, CancellationToken ct = default);

    /// <summary>Obtiene una categoría activa o indica que el recurso no existe (404).</summary>
    Task<CategoryDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Crea una categoría comprobando código único y padre activo.</summary>
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);

    /// <summary>Reemplaza los datos editables y comprueba que no se formen ciclos.</summary>
    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);

    /// <summary>Da de baja una categoría sin hijas pendientes de eliminar.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
