using System;

namespace backend.DTOs.Businesses;

public sealed record PhotoDto(Guid Id, string Url, string? AltText, bool IsPrimary, int SortOrder);
