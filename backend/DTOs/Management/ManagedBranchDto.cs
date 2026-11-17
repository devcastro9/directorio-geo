using backend.DTOs.Businesses;
using System;
using System.Collections.Generic;

namespace backend.DTOs.Management;

public sealed record ManagedBranchDto(
    Guid Id,
    Guid BusinessId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeletedAt,
    BranchInputDto Input,
    IReadOnlyList<PhotoDto> Photos);
