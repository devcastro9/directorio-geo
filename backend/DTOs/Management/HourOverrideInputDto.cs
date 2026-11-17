using System;
using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Management;

public sealed class HourOverrideInputDto
{
    public DateOnly Date { get; init; }
    public bool IsClosed { get; init; }
    public TimeOnly? OpensAt { get; init; }
    public TimeOnly? ClosesAt { get; init; }
    [StringLength(300)]
    public string? Reason { get; init; }
}
