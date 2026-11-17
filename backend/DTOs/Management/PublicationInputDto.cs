using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Management;

public sealed class PublicationInputDto
{
    [Required]
    public string Status { get; init; } = null!; // draft|published
}
