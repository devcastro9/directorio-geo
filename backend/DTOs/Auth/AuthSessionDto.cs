namespace backend.DTOs.Auth;

/// <summary>Información de sesión emitida tras login o rotación de token.</summary>
public sealed record AuthSessionDto(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    UserDto User
);
