using backend.Data;
using backend.DTOs.Auth;
using backend.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Immutable;

namespace backend.Services;

/// <summary>
/// Implementación de la lógica de negocio de autenticación, control de sesiones
/// y rotación de tokens usando Identity Core y JwtBearer sin requerir tablas adicionales.
/// </summary>
public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AppDbContext db,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    private const string ProviderName = "DirectorioGeo";
    private const int AccessTokenMinutes = 15;
    private const int RefreshTokenDays = 30;

    /// <inheritdoc />
    public async Task<AuthResult> LoginAsync(LoginInput input, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(input.Email);
        if (user is null || !user.IsActive)
        {
            logger.LogWarning("Login rejected: user not found or inactive.");
            throw new BadHttpRequestException("Credenciales inválidas o cuenta no disponible.", StatusCodes.Status401Unauthorized);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Login rejected: user {UserId} is locked out.", user.Id);
            throw new BadHttpRequestException("Credenciales inválidas o cuenta no disponible.", StatusCodes.Status401Unauthorized);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            logger.LogWarning("Login rejected: invalid password for user {UserId}.", user.Id);
            throw new BadHttpRequestException("Credenciales inválidas o cuenta no disponible.", StatusCodes.Status401Unauthorized);
        }

        // En P1 solo cuentas con rol Admin pueden iniciar sesión (a menos que P2 esté activado).
        bool isP2 = configuration.GetValue<bool>("Features:P2Enabled", false);
        bool isAdmin = await userManager.IsInRoleAsync(user, "Admin");
        if (!isP2 && !isAdmin)
        {
            logger.LogWarning("Login rejected: user {UserId} is not an Admin in P1 phase.", user.Id);
            throw new BadHttpRequestException("Credenciales inválidas o cuenta no disponible.", StatusCodes.Status401Unauthorized);
        }

        // Registrar LastLoginAt
        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        // Crear sesión y token de refresco (32 bytes criptográficos base64url)
        var sessionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var expiresAt = now.AddDays(RefreshTokenDays);

        string refreshToken = GenerateCryptoToken();
        string tokenHashHex = ComputeTokenHashHex(refreshToken);

        var sessionState = new SessionTokenState
        {
            SessionId = sessionId,
            UserId = user.Id,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            RevokedAt = null,
            ActiveTokenHash = tokenHashHex,
            ConsumedTokenHashes = []
        };

        // Persistir en AspNetUserTokens de Identity
        await userManager.SetAuthenticationTokenAsync(user, ProviderName, $"session:{sessionId}", JsonSerializer.Serialize(sessionState));
        await userManager.SetAuthenticationTokenAsync(user, ProviderName, $"rt:{tokenHashHex}", sessionId.ToString());

        var userRoles = await userManager.GetRolesAsync(user);
        var sortedRoles = userRoles.OrderBy(r => r, StringComparer.Ordinal).ToList();
        string accessToken = GenerateJwtToken(user, sessionId, sortedRoles);

        var userDto = new UserDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            FirstName: user.FirstName ?? string.Empty,
            LastName: user.LastName ?? string.Empty,
            Roles: sortedRoles
        );

        var authSession = new AuthSessionDto(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresIn: AccessTokenMinutes * 60,
            User: userDto
        );

        return new AuthResult(authSession, refreshToken, expiresAt - now);
    }

    /// <inheritdoc />
    public async Task LogoutAsync(string? refreshToken, Guid? sessionId, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            string tokenHashHex = ComputeTokenHashHex(refreshToken);
            var tokenRecord = await db.UserTokens.FirstOrDefaultAsync(t =>
                t.LoginProvider == ProviderName && t.Name == $"rt:{tokenHashHex}", ct);

            if (tokenRecord != null && Guid.TryParse(tokenRecord.Value, out var sidFromToken))
            {
                var sessionRecord = await db.UserTokens.FirstOrDefaultAsync(t =>
                    t.UserId == tokenRecord.UserId && t.LoginProvider == ProviderName && t.Name == $"session:{sidFromToken}", ct);

                if (sessionRecord != null && !string.IsNullOrWhiteSpace(sessionRecord.Value))
                {
                    var state = JsonSerializer.Deserialize<SessionTokenState>(sessionRecord.Value);
                    if (state != null && state.RevokedAt is null)
                    {
                        state.RevokedAt = now;
                        sessionRecord.Value = JsonSerializer.Serialize(state);
                        await db.SaveChangesAsync(ct);
                    }
                }
            }
        }

        if (sessionId.HasValue && sessionId.Value != Guid.Empty)
        {
            var sessionRecords = await db.UserTokens.Where(t =>
                t.LoginProvider == ProviderName && t.Name == $"session:{sessionId.Value}").ToListAsync(ct);

            foreach (var sessionRecord in sessionRecords)
            {
                if (!string.IsNullOrWhiteSpace(sessionRecord.Value))
                {
                    var state = JsonSerializer.Deserialize<SessionTokenState>(sessionRecord.Value);
                    if (state != null && state.RevokedAt is null)
                    {
                        state.RevokedAt = now;
                        sessionRecord.Value = JsonSerializer.Serialize(state);
                    }
                }
            }
            await db.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<UserDto> GetCurrentUserAsync(Guid userId, Guid sessionId, CancellationToken ct)
    {
        var sessionRecord = await db.UserTokens.FirstOrDefaultAsync(t =>
            t.UserId == userId && t.LoginProvider == ProviderName && t.Name == $"session:{sessionId}", ct);

        if (sessionRecord is null || string.IsNullOrWhiteSpace(sessionRecord.Value))
        {
            throw new BadHttpRequestException("Sesión no encontrada.", StatusCodes.Status401Unauthorized);
        }

        var sessionState = JsonSerializer.Deserialize<SessionTokenState>(sessionRecord.Value);
        if (sessionState is null || sessionState.RevokedAt.HasValue || sessionState.ExpiresAt <= DateTime.UtcNow)
        {
            throw new BadHttpRequestException("Sesión revocada o expirada.", StatusCodes.Status401Unauthorized);
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            throw new BadHttpRequestException("Cuenta inactiva o bloqueada.", StatusCodes.Status401Unauthorized);
        }

        var userRoles = await userManager.GetRolesAsync(user);
        var sortedRoles = userRoles.OrderBy(r => r, StringComparer.Ordinal).ToList();

        return new UserDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            FirstName: user.FirstName ?? string.Empty,
            LastName: user.LastName ?? string.Empty,
            Roles: sortedRoles
        );
    }

    /// <inheritdoc />
    public async Task<UserDto> RegisterAsync(RegisterInput input, CancellationToken ct)
    {
        bool isP2 = configuration.GetValue<bool>("Features:P2Enabled", false);
        if (!isP2)
        {
            throw new BadHttpRequestException("La ruta solicitada no está disponible en este entorno.", StatusCodes.Status404NotFound);
        }

        string pwd = input.Password;
        if (!pwd.Any(char.IsUpper) || !pwd.Any(char.IsLower) || !pwd.Any(char.IsDigit) || !pwd.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            throw new ValidationException("La contraseña debe incluir mayúscula, minúscula, dígito y símbolo.");
        }

        var existingUser = await userManager.FindByEmailAsync(input.Email);
        if (existingUser != null)
        {
            throw new BadHttpRequestException("El correo electrónico ya se encuentra registrado.", StatusCodes.Status409Conflict);
        }

        var now = DateTime.UtcNow;
        var newUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = input.Email.Trim().ToUpperInvariant(),
            Email = input.Email,
            FirstName = input.FirstName,
            LastName = input.LastName,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            LastLoginAt = now
        };

        var result = await userManager.CreateAsync(newUser, input.Password);
        if (!result.Succeeded)
        {
            string err = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new BadHttpRequestException(err, StatusCodes.Status400BadRequest);
        }

        await userManager.AddToRoleAsync(newUser, "User");

        return new UserDto(
            Id: newUser.Id,
            Email: newUser.Email,
            FirstName: newUser.FirstName,
            LastName: newUser.LastName,
            Roles: ["User"]
        );
    }

    private string GenerateJwtToken(ApplicationUser user, Guid sessionId, IReadOnlyList<string> roles)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("sid", sessionId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty)
        };

        foreach (var role in roles)
        {
            claims.Add(new(ClaimTypes.Role, role));
            claims.Add(new("roles", role));
        }

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(AccessTokenMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateCryptoToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncoder.Encode(bytes);
    }

    /// <inheritdoc />
    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        string tokenHashHex = ComputeTokenHashHex(refreshToken);

        // Buscar el token en UserTokens
        var tokenRecord = await db.UserTokens.FirstOrDefaultAsync(t =>
            t.LoginProvider == ProviderName && t.Name == $"rt:{tokenHashHex}", ct);

        if (tokenRecord is null || !Guid.TryParse(tokenRecord.Value, out var sessionId))
        {
            throw new BadHttpRequestException("Token de refresco inválido.", StatusCodes.Status401Unauthorized);
        }

        var sessionRecord = await db.UserTokens.FirstOrDefaultAsync(t =>
            t.UserId == tokenRecord.UserId && t.LoginProvider == ProviderName && t.Name == $"session:{sessionId}", ct);

        if (sessionRecord is null || string.IsNullOrWhiteSpace(sessionRecord.Value))
        {
            throw new BadHttpRequestException("Sesión no encontrada.", StatusCodes.Status401Unauthorized);
        }

        var sessionState = JsonSerializer.Deserialize<SessionTokenState>(sessionRecord.Value);
        if (sessionState is null || sessionState.RevokedAt.HasValue || sessionState.ExpiresAt <= DateTime.UtcNow)
        {
            throw new BadHttpRequestException("Sesión revocada o expirada.", StatusCodes.Status401Unauthorized);
        }

        // Detectar reuso de token consumido → revocar familia completa
        if (sessionState.ConsumedTokenHashes.Contains(tokenHashHex))
        {
            logger.LogWarning("Refresh token reuse detected for session {SessionId}. Revoking family.", sessionId);
            sessionState.RevokedAt = DateTime.UtcNow;
            sessionRecord.Value = JsonSerializer.Serialize(sessionState);
            await db.SaveChangesAsync(ct);
            throw new BadHttpRequestException("Token de refresco ya utilizado. Sesión revocada.", StatusCodes.Status401Unauthorized);
        }

        // Verificar que es el token activo
        if (sessionState.ActiveTokenHash != tokenHashHex)
        {
            throw new BadHttpRequestException("Token de refresco inválido.", StatusCodes.Status401Unauthorized);
        }

        var user = await userManager.FindByIdAsync(tokenRecord.UserId.ToString());
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            throw new BadHttpRequestException("Cuenta inactiva o bloqueada.", StatusCodes.Status401Unauthorized);
        }

        // Rotar: marcar consumido, emitir nuevo
        var now = DateTime.UtcNow;
        string newRefreshToken = GenerateCryptoToken();
        string newTokenHashHex = ComputeTokenHashHex(newRefreshToken);

        sessionState.ConsumedTokenHashes.Add(tokenHashHex);
        sessionState.ActiveTokenHash = newTokenHashHex;
        sessionRecord.Value = JsonSerializer.Serialize(sessionState);

        // Registrar nuevo rt
        await userManager.SetAuthenticationTokenAsync(user, ProviderName, $"rt:{newTokenHashHex}", sessionId.ToString());

        await db.SaveChangesAsync(ct);

        var userRoles = await userManager.GetRolesAsync(user);
        var sortedRoles = userRoles.OrderBy(r => r, StringComparer.Ordinal).ToList();
        string accessToken = GenerateJwtToken(user, sessionId, sortedRoles);

        var userDto = new UserDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            FirstName: user.FirstName ?? string.Empty,
            LastName: user.LastName ?? string.Empty,
            Roles: sortedRoles
        );

        var authSession = new AuthSessionDto(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresIn: AccessTokenMinutes * 60,
            User: userDto
        );

        TimeSpan remaining = sessionState.ExpiresAt - now;
        return new AuthResult(authSession, newRefreshToken, remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
    }

    /// <inheritdoc />
    public async Task<UserListDto> GetUsersAsync(UserListRequest request, CancellationToken ct)
    {
        var query = userManager.Users.AsNoTracking();

        if (request.IsActive.HasValue)
            query = query.Where(u => u.IsActive == request.IsActive.Value);

        string? q = request.Q?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            string lower = q.ToLowerInvariant();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(lower)) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(lower)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(lower)));
        }

        // Filtro por rol: cargar IDs del rol desde RoleManager si se especificó
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var usersInRole = await userManager.GetUsersInRoleAsync(request.Role);
            var ids = usersInRole.Select(u => u.Id).ToHashSet();
            query = query.Where(u => ids.Contains(u.Id));
        }

        int total = await query.CountAsync(ct);
        long offset = ((long)request.Page - 1) * request.PageSize;

        if (offset >= total)
            return new UserListDto([], request.Page, request.PageSize, total);

        var pagedUsers = await query
            .OrderBy(u => u.Email)
            .ThenBy(u => u.Id)
            .Skip((int)offset)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var items = new List<UserDto>(pagedUsers.Count);
        foreach (var u in pagedUsers)
        {
            var roles = await userManager.GetRolesAsync(u);
            items.Add(new UserDto(
                Id: u.Id,
                Email: u.Email ?? string.Empty,
                FirstName: u.FirstName ?? string.Empty,
                LastName: u.LastName ?? string.Empty,
                Roles: roles.OrderBy(r => r, StringComparer.Ordinal).ToList()
            ));
        }

        return new UserListDto(items, request.Page, request.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<UserDto> SetUserRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct)
    {
        // Roles permitidos en el sistema
        var allowedRoles = new HashSet<string>(StringComparer.Ordinal) { "Admin", "BusinessOwner", "User" };
        foreach (var role in roles)
        {
            if (!allowedRoles.Contains(role))
                throw new BadHttpRequestException($"El rol '{role}' no es válido.", StatusCodes.Status400BadRequest);
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new BadHttpRequestException("El usuario no fue encontrado.", StatusCodes.Status404NotFound);

        var currentRoles = await userManager.GetRolesAsync(user);
        var desiredRoles = new HashSet<string>(roles, StringComparer.Ordinal);

        var toRemove = currentRoles.Where(r => !desiredRoles.Contains(r)).ToList();
        var toAdd = desiredRoles.Where(r => !currentRoles.Contains(r)).ToList();

        if (toRemove.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, toRemove);
            if (!removeResult.Succeeded)
            {
                string err = string.Join("; ", removeResult.Errors.Select(e => e.Description));
                throw new BadHttpRequestException(err, StatusCodes.Status400BadRequest);
            }
        }

        if (toAdd.Count > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, toAdd);
            if (!addResult.Succeeded)
            {
                string err = string.Join("; ", addResult.Errors.Select(e => e.Description));
                throw new BadHttpRequestException(err, StatusCodes.Status400BadRequest);
            }
        }

        var updatedRoles = await userManager.GetRolesAsync(user);
        return new UserDto(
            Id: user.Id,
            Email: user.Email ?? string.Empty,
            FirstName: user.FirstName ?? string.Empty,
            LastName: user.LastName ?? string.Empty,
            Roles: updatedRoles.OrderBy(r => r, StringComparer.Ordinal).ToList()
        );
    }

    private static string ComputeTokenHashHex(string token)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
