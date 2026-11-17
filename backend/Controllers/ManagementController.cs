using backend.DTOs;
using backend.DTOs.Management;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Controllers;

/// <summary>Gestión administrativa de negocios, sucursales y fotos.</summary>
[Authorize(Roles = "Admin")]
public sealed class ManagementController(
    IBusinessService businessService,
    ILogger<ManagementController> logger) : ApiControllerBase
{
    // ==========================================
    // NEGOCIOS (IDs 11–17)
    // ==========================================

    /// <summary>11: Lista negocios en gestión con filtro de estado y paginación.</summary>
    [HttpGet("businesses")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ManagedSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ManagedSummaryDto>>>> ListBusinesses(
        [FromQuery] ManagementListRequest request, CancellationToken ct)
    {
        logger.LogDebug("Listing managed businesses: page {Page}, pageSize {PageSize}.", request.Page, request.PageSize);
        PagedResult<ManagedSummaryDto> result = await businessService.ListAsync(request, ct);
        return Ok(new ApiResponse<PagedResult<ManagedSummaryDto>>(result));
    }

    /// <summary>12: Da de alta un negocio con su primera sucursal principal en estado borrador.</summary>
    [HttpPost("businesses")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<ManagedBusinessDto>), StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<ManagedBusinessDto>>> CreateBusiness(
        [FromBody] CreateBusinessRequest request, CancellationToken ct)
    {
        logger.LogInformation("Creating new managed business '{TradeName}'.", request.Business.TradeName);
        ManagedBusinessDto created = await businessService.CreateAsync(request, ct);

        Response.Headers["X-Created-Resource-Id"] = created.Id.ToString();
        return CreatedAtAction(
            nameof(GetBusiness),
            new { businessId = created.Id },
            new ApiResponse<ManagedBusinessDto>(created));
    }

    /// <summary>13: Obtiene el detalle administrativo completo de un negocio.</summary>
    [HttpGet("businesses/{businessId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ManagedBusinessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<ManagedBusinessDto>>> GetBusiness(Guid businessId, CancellationToken ct)
    {
        logger.LogDebug("Retrieving managed business {BusinessId}.", businessId);
        ManagedBusinessDto business = await businessService.GetAsync(businessId, ct);
        return Ok(new ApiResponse<ManagedBusinessDto>(business));
    }

    /// <summary>14: Actualiza los datos editables de un negocio.</summary>
    [HttpPut("businesses/{businessId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<ManagedBusinessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<ManagedBusinessDto>>> UpdateBusiness(
        Guid businessId, [FromBody] BusinessInputDto input, CancellationToken ct)
    {
        logger.LogInformation("Updating managed business {BusinessId}.", businessId);
        ManagedBusinessDto updated = await businessService.UpdateAsync(businessId, input, ct);
        return Ok(new ApiResponse<ManagedBusinessDto>(updated));
    }

    /// <summary>15: Cambia el estado de publicación del negocio (draft o published).</summary>
    [HttpPut("businesses/{businessId:guid}/publication")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<ManagedBusinessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<ManagedBusinessDto>>> SetPublication(
        Guid businessId, [FromBody] PublicationInputDto input, CancellationToken ct)
    {
        logger.LogInformation("Setting publication status of business {BusinessId} to '{Status}'.", businessId, input.Status);
        ManagedBusinessDto updated = await businessService.SetPublicationAsync(businessId, input, ct);
        return Ok(new ApiResponse<ManagedBusinessDto>(updated));
    }

    /// <summary>16: Baja lógica (archivado) de un negocio y sus sucursales.</summary>
    [HttpDelete("businesses/{businessId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteBusiness(Guid businessId, CancellationToken ct)
    {
        logger.LogInformation("Archiving managed business {BusinessId}.", businessId);
        await businessService.SoftDeleteAsync(businessId, ct);
        return NoContent();
    }

    /// <summary>17: Restaura un negocio archivado junto con las sucursales seleccionadas.</summary>
    [HttpPost("businesses/{businessId:guid}/restore")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<ManagedBusinessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<ManagedBusinessDto>>> RestoreBusiness(
        Guid businessId, [FromBody] RestoreInputDto input, CancellationToken ct)
    {
        logger.LogInformation("Restoring managed business {BusinessId}.", businessId);
        ManagedBusinessDto restored = await businessService.RestoreAsync(businessId, input, ct);
        return Ok(new ApiResponse<ManagedBusinessDto>(restored));
    }

    // ==========================================
    // SUCURSALES (IDs 18–21)
    // ==========================================

    /// <summary>18: Añade una nueva sucursal al negocio especificado.</summary>
    [HttpPost("businesses/{businessId:guid}/branches")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<ManagedBusinessDto>), StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<ManagedBusinessDto>>> AddBranch(
        Guid businessId, [FromBody] BranchInputDto input, CancellationToken ct)
    {
        logger.LogInformation("Adding branch '{BranchName}' to business {BusinessId}.", input.Name, businessId);
        var (updatedBusiness, branchId) = await businessService.AddBranchAsync(businessId, input, ct);

        Response.Headers["X-Created-Resource-Id"] = branchId.ToString();
        return CreatedAtAction(
            nameof(GetBusiness),
            new { businessId },
            new ApiResponse<ManagedBusinessDto>(updatedBusiness));
    }

    /// <summary>19: Reemplaza completamente los datos y colecciones de una sucursal.</summary>
    [HttpPut("branches/{branchId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<ManagedBusinessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<ManagedBusinessDto>>> UpdateBranch(
        Guid branchId, [FromBody] BranchInputDto input, CancellationToken ct)
    {
        logger.LogInformation("Updating branch {BranchId}.", branchId);
        ManagedBusinessDto updated = await businessService.UpdateBranchAsync(branchId, input, ct);
        return Ok(new ApiResponse<ManagedBusinessDto>(updated));
    }

    /// <summary>20: Baja lógica de una sucursal.</summary>
    [HttpDelete("branches/{branchId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> DeleteBranch(Guid branchId, CancellationToken ct)
    {
        logger.LogInformation("Soft-deleting branch {BranchId}.", branchId);
        await businessService.SoftDeleteBranchAsync(branchId, ct);
        return NoContent();
    }

    /// <summary>21: Restaura una sucursal previamente dada de baja.</summary>
    [HttpPost("branches/{branchId:guid}/restore")]
    [ProducesResponseType(typeof(ApiResponse<ManagedBusinessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<ManagedBusinessDto>>> RestoreBranch(Guid branchId, CancellationToken ct)
    {
        logger.LogInformation("Restoring branch {BranchId}.", branchId);
        ManagedBusinessDto restored = await businessService.RestoreBranchAsync(branchId, ct);
        return Ok(new ApiResponse<ManagedBusinessDto>(restored));
    }

    // ==========================================
    // FOTOS (IDs 22–24)
    // ==========================================

    /// <summary>22: Sube una foto a la sucursal indicada.</summary>
    [HttpPost("branches/{branchId:guid}/photos")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ManagedBusinessDto>), StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<ManagedBusinessDto>>> AddPhoto(
        Guid branchId,
        IFormFile file,
        [FromForm] PhotoInputDto meta,
        CancellationToken ct)
    {
        logger.LogInformation("Uploading photo for branch {BranchId}.", branchId);
        var (updatedBusiness, photoId) = await businessService.AddPhotoAsync(branchId, file, meta, ct);

        Response.Headers["X-Created-Resource-Id"] = photoId.ToString();
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<ManagedBusinessDto>(updatedBusiness));
    }

    /// <summary>23: Actualiza los metadatos de una foto (altText, sortOrder, portada).</summary>
    [HttpPut("photos/{photoId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<ManagedBusinessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<ManagedBusinessDto>>> UpdatePhoto(
        Guid photoId, [FromBody] PhotoInputDto input, CancellationToken ct)
    {
        logger.LogInformation("Updating photo {PhotoId}.", photoId);
        ManagedBusinessDto updated = await businessService.UpdatePhotoMetaAsync(photoId, input, ct);
        return Ok(new ApiResponse<ManagedBusinessDto>(updated));
    }

    /// <summary>24: Baja lógica de una foto.</summary>
    [HttpDelete("photos/{photoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePhoto(Guid photoId, CancellationToken ct)
    {
        logger.LogInformation("Soft-deleting photo {PhotoId}.", photoId);
        await businessService.SoftDeletePhotoAsync(photoId, ct);
        return NoContent();
    }
}
