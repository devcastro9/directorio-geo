using backend.DTOs;
using backend.DTOs.Auth;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Controllers;

/// <summary>
/// Controlador de autenticación, emisión de tokens, gestión de sesiones y administración de roles.
/// </summary>
[Route("api/v1/auth")]
public sealed class AuthController(
    IAuthService authService,
    IWebHostEnvironment environment,
    ILogger<AuthController> logger) : ApiControllerBase
{
    private const string RefreshCookieName = "dg_refresh";
    private const string AuthPath = "/api/v1/auth";

    // ==========================================
    // SESIONES Y AUTENTICACIÓN
    // ==========================================

    /// <summary>07: Inicia sesión mediante credenciales. Emite AuthSession y cookie de refresco.</summary>
    [HttpPost("login")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<AuthSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<AuthSessionDto>>> Login(
        [FromBody] LoginInput input, CancellationToken ct)
    {
        SetNoStoreHeaders();
        logger.LogInformation("Attempting login for user.");
        AuthResult result = await authService.LoginAsync(input, ct);
        SetRefreshCookie(result.RefreshToken, result.CookieMaxAge);
        return Ok(new ApiResponse<AuthSessionDto>(result.Session));
    }

    /// <summary>08: Rota el refresh token. Emite nueva AuthSession y nueva cookie.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<AuthSessionDto>>> Refresh(CancellationToken ct)
    {
        SetNoStoreHeaders();

        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var cookieToken) || string.IsNullOrWhiteSpace(cookieToken))
        {
            DeleteRefreshCookie();
            throw new Microsoft.AspNetCore.Http.BadHttpRequestException(
                "Cookie de refresco ausente o inválida.", StatusCodes.Status401Unauthorized);
        }

        logger.LogInformation("Rotating refresh token.");
        AuthResult result = await authService.RefreshAsync(cookieToken, ct);
        SetRefreshCookie(result.RefreshToken, result.CookieMaxAge);
        return Ok(new ApiResponse<AuthSessionDto>(result.Session));
    }

    /// <summary>09: Cierra la sesión activa y elimina la cookie de refresco.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        SetNoStoreHeaders();

        Request.Cookies.TryGetValue(RefreshCookieName, out var cookieToken);

        Guid? sessionId = null;
        if (Guid.TryParse(User.FindFirst("sid")?.Value, out var parsedSid))
            sessionId = parsedSid;

        await authService.LogoutAsync(cookieToken, sessionId, ct);
        DeleteRefreshCookie();
        return NoContent();
    }

    /// <summary>10: Obtiene la información del usuario en sesión activa.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Me(CancellationToken ct)
    {
        SetNoStoreHeaders();

        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(subClaim, out var userId) ||
            !Guid.TryParse(User.FindFirst("sid")?.Value, out var sessionId))
        {
            Response.Headers.WWWAuthenticate = "Bearer";
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Identificador de usuario o sesión no válido en las credenciales."
            });
        }

        UserDto user = await authService.GetCurrentUserAsync(userId, sessionId, ct);
        return Ok(new ApiResponse<UserDto>(user));
    }

    /// <summary>28: Registro de nuevo usuario (disponible a partir de fase P2).</summary>
    [HttpPost("register")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Register(
        [FromBody] RegisterInput input, CancellationToken ct)
    {
        SetNoStoreHeaders();
        UserDto user = await authService.RegisterAsync(input, ct);
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<UserDto>(user));
    }

    // ==========================================
    // GESTIÓN DE USUARIOS Y ROLES (Admin)
    // ==========================================

    /// <summary>Lista usuarios con paginación y filtros opcionales. Solo Admin.</summary>
    [HttpGet("users")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<UserListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserListDto>>> GetUsers(
        [FromQuery] UserListRequest request, CancellationToken ct)
    {
        logger.LogDebug("Listing users: page {Page}, role {Role}.", request.Page, request.Role);
        UserListDto result = await authService.GetUsersAsync(request, ct);
        return Ok(new ApiResponse<UserListDto>(result));
    }

    /// <summary>Reemplaza los roles de un usuario. Solo Admin.</summary>
    [HttpPut("users/{userId:guid}/roles")]
    [Authorize(Roles = "Admin")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ApiResponse<UserDto>>> SetUserRoles(
        Guid userId, [FromBody] SetUserRolesRequest request, CancellationToken ct)
    {
        logger.LogInformation("Setting roles for user {UserId}: [{Roles}].", userId, string.Join(", ", request.Roles));
        UserDto user = await authService.SetUserRolesAsync(userId, request.Roles, ct);
        return Ok(new ApiResponse<UserDto>(user));
    }

    // ==========================================
    // HELPERS PRIVADOS
    // ==========================================

    private void SetNoStoreHeaders()
    {
        Response.Headers.CacheControl = "no-store";
    }

    private void SetRefreshCookie(string token, TimeSpan maxAge)
    {
        bool isSecure = !environment.IsDevelopment() || Request.IsHttps;
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = SameSiteMode.Strict,
            Path = AuthPath,
            MaxAge = maxAge
        });
    }

    private void DeleteRefreshCookie()
    {
        bool isSecure = !environment.IsDevelopment() || Request.IsHttps;
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = SameSiteMode.Strict,
            Path = AuthPath
        });
    }
}
