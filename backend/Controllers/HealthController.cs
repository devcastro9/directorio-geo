using backend.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Controllers;

/// <summary>Endpoints de salud del servicio.</summary>
[Route("api/v1/health")]
[ApiController]
[Produces("application/json")]
public sealed class HealthController(AppDbContext db, ILogger<HealthController> logger) : ControllerBase
{
    private record HealthResponse(string Status);

    /// <summary>26: Salud del proceso, sin verificar DB.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new HealthResponse("ok"));
    }

    /// <summary>27: Salud completa: verifica conexión a PostgreSQL y extensiones requeridas.</summary>
    [HttpGet("ready")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        try
        {
            // Verificar conectividad y extensiones requeridas
            await db.Database.ExecuteSqlRawAsync(
                "SELECT extname FROM pg_extension WHERE extname IN ('postgis','unaccent','pg_trgm')",
                ct);

            return Ok(new HealthResponse("ok"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Health ready check failed.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthResponse("unavailable"));
        }
    }
}
