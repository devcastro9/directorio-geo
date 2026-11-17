using System;

namespace backend.DTOs.Businesses;

public sealed record OpeningHourDto(int Weekday, TimeOnly OpensAt, TimeOnly ClosesAt);
