using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Management;

public sealed class PhotoInputDto
{
    [StringLength(300)]
    public string? AltText { get; init; }

    public bool IsPrimary { get; init; }

    [Range(0, 999)]
    public int SortOrder { get; init; }
}
