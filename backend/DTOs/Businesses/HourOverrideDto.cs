using System;

namespace backend.DTOs.Businesses;

public sealed record HourOverrideDto(DateOnly Date, bool IsClosed, TimeOnly? OpensAt, TimeOnly? ClosesAt, string? Reason);
