using backend.DTOs;
using backend.DTOs.Businesses;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Controllers;

/// <summary>Consulta pública de negocios.</summary>
public sealed class BusinessesController(
    IBranchSearchService searchService,
    ILogger<BusinessesController> logger) : ApiControllerBase
{
    /// <summary>Obtiene la ficha pública completa de un negocio y sus sucursales por su slug.</summary>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(ApiResponse<PublicBusinessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<PublicBusinessDto>>> GetBySlug(string slug, CancellationToken ct)
    {
        logger.LogDebug("Retrieving public business by slug '{Slug}'.", slug);
        PublicBusinessDto business = await searchService.GetBySlugAsync(slug, ct);
        return Ok(new ApiResponse<PublicBusinessDto>(business));
    }
}
