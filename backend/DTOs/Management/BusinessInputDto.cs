using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Management;

public sealed class BusinessInputDto
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string TradeName { get; init; } = null!;

    [StringLength(5000)]
    public string? Description { get; init; }

    [Required]
    public string Status { get; init; } = null!; // active|temporarily_closed

    [Range(1, 4)]
    public int? PriceLevel { get; init; }

    [StringLength(250)]
    public string? LegalName { get; init; }

    [StringLength(50)]
    public string? TaxId { get; init; }
}
