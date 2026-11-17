using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Management;

public sealed class ManagementListRequest
{
    [StringLength(200, MinimumLength = 1)]
    public string? Q { get; init; }

    public string? PublicationStatus { get; init; } // draft|published|archived

    public bool Deleted { get; init; } = false;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}
