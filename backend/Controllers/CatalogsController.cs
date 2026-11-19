using backend.Data;
using backend.DTOs;
using backend.DTOs.Catalogs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Controllers;

/// <summary>Catálogos de referencia: servicios, características y métodos de pago.</summary>
public sealed class CatalogsController(AppDbContext db, ILogger<CatalogsController> logger) : ApiControllerBase
{
    private const int MaxCatalogItems = 500;

    /// <summary>01: Devuelve todos los catálogos activos para carga inicial del cliente.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CatalogsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CatalogsDto>>> Get(CancellationToken ct)
    {
        logger.LogDebug("Loading all catalogs.");

        var services = await db.ServiceProducts
            .AsNoTracking()
            .Where(s => s.Active && s.DeletedAt == null)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ThenBy(s => s.Id)
            .Take(MaxCatalogItems)
            .Select(s => new CatalogItemDto(s.Id, s.Code, s.Name, s.SortOrder))
            .ToListAsync(ct);

        var features = await db.Features
            .AsNoTracking()
            .Where(f => f.Active && f.DeletedAt == null)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ThenBy(f => f.Id)
            .Take(MaxCatalogItems)
            .Select(f => new CatalogItemDto(f.Id, f.Code, f.Name, f.SortOrder))
            .ToListAsync(ct);

        var paymentMethods = await db.PaymentMethods
            .AsNoTracking()
            .Where(p => p.Active && p.DeletedAt == null)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ThenBy(p => p.Id)
            .Take(MaxCatalogItems)
            .Select(p => new CatalogItemDto(p.Id, p.Code, p.Name, p.SortOrder))
            .ToListAsync(ct);

        var catalogs = new CatalogsDto(services, features, paymentMethods);
        return Ok(new ApiResponse<CatalogsDto>(catalogs));
    }
}
