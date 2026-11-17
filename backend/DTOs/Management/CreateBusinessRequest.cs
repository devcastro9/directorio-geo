using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Management;

public sealed class CreateBusinessRequest
{
    [Required]
    public BusinessInputDto Business { get; init; } = null!;

    [Required]
    public BranchInputDto Branch { get; init; } = null!;
}
