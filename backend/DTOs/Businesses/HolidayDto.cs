using System;

namespace backend.DTOs.Businesses;

public sealed record HolidayDto(DateOnly Date, string Name);
