using backend.DTOs;
using backend.DTOs.Categories;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Controllers;

/// <summary>Consulta y mantenimiento de categorías del catálogo.</summary>
[ApiController]
[Route("api/categories")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
public sealed class CategoriesController(ICategoryService service, ILogger<CategoriesController> logger) : ControllerBase
{
    /// <summary>Lista categorías activas; search busca texto literal en nombre y código.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<CategoryDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] ListRequest request, CancellationToken ct)
    {
        logger.LogDebug("Listado de categorías solicitado: página {Page}.", request.Page);
        return Ok(new ApiResponse<PagedResult<CategoryDto>>(await service.ListAsync(request, ct)));
    }

    /// <summary>Obtiene una categoría activa por su identificador.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<CategoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        logger.LogDebug("Consulta de categoría {CategoryId}.", id);
        return Ok(new ApiResponse<CategoryDto>(await service.GetAsync(id, ct)));
    }

    /// <summary>Crea una categoría activa con código único.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ApiResponse<CategoryDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create([FromBody, Required] CreateCategoryRequest request, CancellationToken ct)
    {
        CategoryDto category = await service.CreateAsync(request, ct);
        logger.LogInformation("Creación HTTP de categoría {CategoryId}.", category.Id);
        return CreatedAtAction(nameof(Get), new { id = category.Id }, new ApiResponse<CategoryDto>(category));
    }

    /// <summary>Reemplaza los datos editables de una categoría activa.</summary>
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType<ApiResponse<CategoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Update(Guid id, [FromBody, Required] UpdateCategoryRequest request, CancellationToken ct)
    {
        CategoryDto category = await service.UpdateAsync(id, request, ct);
        logger.LogInformation("Actualización HTTP de categoría {CategoryId}.", id);
        return Ok(new ApiResponse<CategoryDto>(category));
    }

    /// <summary>Da de baja una categoría; devuelve 409 si tiene hijas sin eliminar.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType<ApiResponse<bool>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        logger.LogInformation("Baja lógica HTTP de categoría {CategoryId}.", id);
        return Ok(new ApiResponse<bool>(true));
    }
}
