using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Management;

public sealed class BranchInputDto
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = null!;

    [Required, StringLength(1000, MinimumLength = 1)]
    public string Address { get; init; } = null!;

    [Required]
    public Guid TerritoryId { get; init; }

    [Required]
    public PointDto Location { get; init; } = null!;

    [Required]
    public string Status { get; init; } = null!; // active|temporarily_closed

    public bool IsPrimary { get; set; }

    [Range(1, 4)]
    public int? PriceLevel { get; init; }

    public decimal? MinPriceBob { get; init; }
    public decimal? MaxPriceBob { get; init; }

    public IReadOnlyList<Guid> CategoryIds { get; init; } = [];
    public IReadOnlyList<Guid> ServiceIds { get; init; } = [];
    public IReadOnlyList<Guid> FeatureIds { get; init; } = [];
    public IReadOnlyList<Guid> PaymentMethodIds { get; init; } = [];
    public IReadOnlyList<ContactInputDto> Contacts { get; init; } = [];

    public bool HoursKnown { get; init; }
    public IReadOnlyList<OpeningHourInputDto> OpeningHours { get; init; } = [];
    public IReadOnlyList<HourOverrideInputDto> HourOverrides { get; init; } = [];
}
