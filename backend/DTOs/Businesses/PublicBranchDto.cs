using System;
using System.Collections.Generic;

namespace backend.DTOs.Businesses;

public sealed record PublicBranchDto(
    Guid Id,
    Guid BusinessId,
    string BusinessSlug,
    string TradeName,
    string Name,
    string Address,
    Guid TerritoryId,
    PointDto Location,
    int? DistanceM,
    string Status,
    bool? IsOpen,
    int? PriceLevel,
    string? CoverPhotoUrl,
    string? Description,
    bool IsPrimary,
    string BusinessStatus,
    string BranchStatus,
    IReadOnlyList<TerritoryRefDto> TerritoryPath,
    IReadOnlyList<CatalogRefDto> Categories,
    IReadOnlyList<CatalogRefDto> Services,
    IReadOnlyList<CatalogRefDto> Features,
    IReadOnlyList<CatalogRefDto> PaymentMethods,
    IReadOnlyList<ContactDto> Contacts,
    bool HoursKnown,
    IReadOnlyList<OpeningHourDto> OpeningHours,
    IReadOnlyList<HourOverrideDto> HourOverrides,
    IReadOnlyList<HolidayDto> Holidays,
    string TimeZone,
    DateTimeOffset EvaluatedAt,
    decimal? MinPriceBob,
    decimal? MaxPriceBob,
    IReadOnlyList<PhotoDto> Photos);
