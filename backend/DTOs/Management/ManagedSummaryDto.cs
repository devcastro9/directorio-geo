using System;

namespace backend.DTOs.Management;

public sealed record ManagedSummaryDto(
    Guid Id,
    string Slug,
    string TradeName,
    string Status,
    string PublicationStatus,
    DateTimeOffset? DeletedAt,
    DateTimeOffset UpdatedAt,
    int BranchCount);
