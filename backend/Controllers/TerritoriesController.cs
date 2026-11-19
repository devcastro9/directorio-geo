using backend.Data;
using backend.DTOs;
using backend.DTOs.Territories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Controllers;

/// <summary>Consulta del árbol territorial de Bolivia.</summary>
public sealed class TerritoriesController(AppDbContext db, ILogger<TerritoriesController> logger) : ApiControllerBase
{
    private static readonly string[] AllowedTypes =
        ["department", "province", "municipality", "zone", "neighborhood"];

    /// <summary>02: Lista territorios activos con filtros opcionales por tipo y padre.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TerritoryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<TerritoryDto>>>> List(
        [FromQuery] TerritoryListRequest request, CancellationToken ct)
    {
        // Validar tipo si se especificó
        if (!string.IsNullOrWhiteSpace(request.Type) && !AllowedTypes.Contains(request.Type))
        {
            throw new BadHttpRequestException(
                $"El tipo de territorio '{request.Type}' no es válido.",
                StatusCodes.Status400BadRequest);
        }

        // Validar paginación
        long offset = ((long)request.Page - 1) * request.PageSize;
        if (offset > 2000)
        {
            throw new BadHttpRequestException(
                "El desplazamiento de paginación excede el límite máximo permitido de 2000.",
                StatusCodes.Status400BadRequest);
        }

        logger.LogDebug("Listing territories: type={Type}, parentId={ParentId}, page={Page}.",
            request.Type, request.ParentId, request.Page);

        var query = db.Territories.AsNoTracking()
            .Where(t => t.Active && t.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(request.Type))
            query = query.Where(t => t.TerritoryType == request.Type);

        if (request.ParentId.HasValue)
        {
            // Validar que el padre exista y esté activo
            bool parentExists = await db.Territories
                .AsNoTracking()
                .AnyAsync(t => t.Id == request.ParentId.Value && t.Active && t.DeletedAt == null, ct);

            if (!parentExists)
                throw new BadHttpRequestException(
                    "El territorio padre no existe o no está activo.",
                    StatusCodes.Status400BadRequest);

            query = query.Where(t => t.ParentId == request.ParentId.Value);
        }

        int total = await query.CountAsync(ct);

        if (offset >= total)
            return Ok(new ApiResponse<PagedResult<TerritoryDto>>(
                new PagedResult<TerritoryDto>([], request.Page, request.PageSize, total)));

        var items = await query
            .OrderBy(t => t.Name).ThenBy(t => t.Id)
            .Skip((int)offset)
            .Take(request.PageSize)
            .Select(t => new
            {
                t.Id, t.ParentId, t.TerritoryType, t.Name, t.CodeIne, t.Boundary
            })
            .ToListAsync(ct);

        var dtos = items.Select(t => new TerritoryDto(
            t.Id,
            t.ParentId,
            t.TerritoryType,
            t.Name,
            t.CodeIne,
            GetBounds(t.Boundary)
        )).ToList();

        return Ok(new ApiResponse<PagedResult<TerritoryDto>>(
            new PagedResult<TerritoryDto>(dtos, request.Page, request.PageSize, total)));
    }

    /// Extrae [minLng, minLat, maxLng, maxLat] del bounding box de la geometría.
    private static double[]? GetBounds(MultiPolygon? boundary)
    {
        if (boundary is null) return null;
        var env = boundary.EnvelopeInternal;
        return [env.MinX, env.MinY, env.MaxX, env.MaxY];
    }
}
