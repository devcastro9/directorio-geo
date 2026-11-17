using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Controllers;

/// <summary>Descarga y entrega de binarios de fotos.</summary>
[Route("api/v1/photos")]
public sealed class PhotosController(
    IBusinessService businessService,
    ILogger<PhotosController> logger) : ApiControllerBase
{
    /// <summary>06: Obtiene el binario de una foto. Público si está publicada; preview requiere sesión.</summary>
    [HttpGet("{photoId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetPhoto(Guid photoId, CancellationToken ct)
    {
        logger.LogDebug("Serving photo {PhotoId}.", photoId);

        bool isAdmin = User.IsInRole("Admin");
        var (data, contentType) = await businessService.GetPhotoAsync(photoId, isAdmin, ct);

        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] = "inline";

        return File(data, contentType);
    }
}
