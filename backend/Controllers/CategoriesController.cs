using backend.DTOs;
using backend.DTOs.Categories;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Controllers;

/// <summary>Consulta y administración de categorías del catálogo.</summary>
public sealed class CategoriesController(ICategoryService service, ILogger<CategoriesController> logger) : ApiControllerBase
{
    /// <summary>Lista categorías activas; search busca texto literal en nombre y código.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<CategoryDto>>>> List([FromQuery] ListRequest request, CancellationToken ct)
    {
        logger.LogDebug("Retrieving categories page {Page} with size {PageSize}.", request.Page, request.PageSize);
        PagedResult<CategoryDto> result = await service.ListAsync(request, ct);
        return Ok(new ApiResponse<PagedResult<CategoryDto>>(result));
    }

    /// <summary>Obtiene una categoría activa por su identificador único.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Get(Guid id, CancellationToken ct)
    {
        logger.LogDebug("Retrieving category {CategoryId}.", id);
        CategoryDto category = await service.GetAsync(id, ct);
        return Ok(new ApiResponse<CategoryDto>(category));
    }

    /// <summary>Crea una nueva categoría activa en el catálogo.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Create(CreateCategoryRequest request, CancellationToken ct)
    {
        logger.LogInformation("Creating category with code '{CategoryCode}'.", request.Code);
        CategoryDto category = await service.CreateAsync(request, ct);
        logger.LogInformation("Category {CategoryId} created successfully.", category.Id);

        return CreatedAtAction(
            nameof(Get),
            new { id = category.Id },
            new ApiResponse<CategoryDto>(category));
    }

    /// <summary>Reemplaza los datos editables de una categoría activa.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [Consumes("application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> Update(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken ct)
    {
        logger.LogInformation("Updating category {CategoryId}.", id);
        CategoryDto category = await service.UpdateAsync(id, request, ct);
        logger.LogInformation("Category {CategoryId} updated successfully.", id);

        return Ok(new ApiResponse<CategoryDto>(category));
    }

    /// <summary>Realiza la baja lógica de una categoría activa si no tiene hijas sin eliminar.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken ct)
    {
        logger.LogInformation("Soft-deleting category {CategoryId}.", id);
        await service.DeleteAsync(id, ct);
        logger.LogInformation("Category {CategoryId} soft-deleted successfully.", id);

        return NoContent();
    }
}
