using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Management;

public sealed class RestoreInputDto
{
    [Required, MinLength(1), MaxLength(100)]
    public IReadOnlyList<Guid> BranchIds { get; init; } = [];
}
