using System;
using System.Collections.Generic;

namespace backend.DTOs.Businesses;

public sealed record PublicBusinessDto(
    Guid Id,
    string Slug,
    string TradeName,
    string? Description,
    string Status,
    int? PriceLevel,
    IReadOnlyList<PublicBranchDto> Branches);
