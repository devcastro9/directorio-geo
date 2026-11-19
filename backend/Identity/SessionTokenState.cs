using System;
using System.Collections.Generic;

namespace backend.Identity;

/// <summary>
/// Estado serializado de la sesión persistida en AspNetUserTokens.
/// No crea tablas adicionales en la base de datos; aprovecha las capacidades
/// nativas de Microsoft.AspNetCore.Identity.EntityFrameworkCore.
/// </summary>
public sealed class SessionTokenState
{
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string ActiveTokenHash { get; set; } = string.Empty;
    public List<string> ConsumedTokenHashes { get; set; } = new();
}
