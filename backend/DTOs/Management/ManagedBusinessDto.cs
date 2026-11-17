using System;
using System.Collections.Generic;

namespace backend.DTOs.Management;

public sealed record ManagedBusinessDto(
    Guid Id,
    string Slug,
    string TradeName,
    string? Description,
    string Status,
    int? PriceLevel,
    string PublicationStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt,
    IReadOnlyList<ManagedBranchDto> Branches,
    PrivateDataDto? PrivateData);
