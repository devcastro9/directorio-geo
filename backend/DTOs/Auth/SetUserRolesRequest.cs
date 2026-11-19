using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Auth;

public sealed class SetUserRolesRequest
{
    [Required]
    public IReadOnlyList<string> Roles { get; init; } = [];
}
