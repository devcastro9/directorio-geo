using backend.DTOs;
using backend.DTOs.Branches;
using backend.DTOs.Businesses;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Controllers;

/// <summary>Búsqueda pública y consulta de sucursales geolocalizadas.</summary>
public sealed class BranchesController(
    IBranchSearchService searchService,
    ILogger<BranchesController> logger) : ApiControllerBase
{
    /// <summary>Búsqueda pública de sucursales en formato JSON o GeoJSON.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BranchSummaryDto>>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(GeoJsonFeatureCollectionDto), StatusCodes.Status200OK, "application/geo+json")]
    public async Task<IActionResult> Search([FromQuery] BranchSearchRequest request, CancellationToken ct)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string format = request.Format?.Trim().ToLowerInvariant() ?? "json";

        if (format == "geojson")
        {
            logger.LogDebug("Searching branches in GeoJSON format.");
            GeoJsonFeatureCollectionDto geojson = await searchService.SearchGeoJsonAsync(request, now, ct);
            return StatusCode(StatusCodes.Status200OK, geojson);
        }

        logger.LogDebug("Searching branches in JSON format.");
        PagedResult<BranchSummaryDto> result = await searchService.SearchAsync(request, now, ct);
        return Ok(new ApiResponse<PagedResult<BranchSummaryDto>>(result));
    }

    /// <summary>Obtiene la ficha pública detallada de una sucursal por su identificador.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PublicBranchDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<PublicBranchDto>>> Get(Guid id, CancellationToken ct)
    {
        logger.LogDebug("Retrieving public branch {BranchId}.", id);
        PublicBranchDto branch = await searchService.GetBranchAsync(id, ct);
        return Ok(new ApiResponse<PublicBranchDto>(branch));
    }
}
