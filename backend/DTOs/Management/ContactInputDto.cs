using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Management;

public sealed class ContactInputDto
{
    [Required]
    public string Type { get; init; } = null!; // phone|whatsapp|email|website|facebook|instagram

    [Required, StringLength(500, MinimumLength = 1)]
    public string Value { get; init; } = null!;
}
