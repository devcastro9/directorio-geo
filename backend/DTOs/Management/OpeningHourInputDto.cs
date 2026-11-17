using System;

namespace backend.DTOs.Management;

public sealed class OpeningHourInputDto
{
    public int Weekday { get; init; } // 0=domingo..6=sábado
    public TimeOnly OpensAt { get; init; }
    public TimeOnly ClosesAt { get; init; }
}
