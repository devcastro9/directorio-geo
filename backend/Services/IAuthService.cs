using backend.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Services;

/// <summary>Resultado de una operación de autenticación con sesión y token de refresco.</summary>
public sealed record AuthResult(
    AuthSessionDto Session,
    string RefreshToken,
    TimeSpan CookieMaxAge
);

/// <summary>Contrato de servicio para operaciones de autenticación y gestión de sesiones.</summary>
public interface IAuthService
{
    /// <summary>Inicia sesión con credenciales, actualiza LastLoginAt y genera una nueva sesión.</summary>
    Task<AuthResult> LoginAsync(LoginInput input, CancellationToken ct);

    /// <summary>Rota el refresh token; revoca el consumido y emite uno nuevo.</summary>
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct);

    /// <summary>Revoca la sesión asociada a la cookie o al identificador de sesión.</summary>
    Task LogoutAsync(string? refreshToken, Guid? sessionId, CancellationToken ct);

    /// <summary>Obtiene el usuario actual comprobando su estado activo y la vigencia de la sesión.</summary>
    Task<UserDto> GetCurrentUserAsync(Guid userId, Guid sessionId, CancellationToken ct);

    /// <summary>Registra una nueva cuenta de usuario con rol User (fase P2).</summary>
    Task<UserDto> RegisterAsync(RegisterInput input, CancellationToken ct);

    /// <summary>Lista usuarios paginados con filtros opcionales.</summary>
    Task<UserListDto> GetUsersAsync(UserListRequest request, CancellationToken ct);

    /// <summary>Reemplaza los roles de un usuario. Solo Admin.</summary>
    Task<UserDto> SetUserRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct);
}
