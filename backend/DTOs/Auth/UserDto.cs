using System;
using System.Collections.Generic;

namespace backend.DTOs.Auth;

/// <summary>Representación canónica del usuario autenticado.</summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles
);
