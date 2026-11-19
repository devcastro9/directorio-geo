using System.ComponentModel.DataAnnotations;

namespace backend.DTOs.Auth;

public sealed class UserListRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    /// <summary>Búsqueda por email, firstName o lastName (substring, insensible a mayúsculas).</summary>
    public string? Q { get; init; }

    /// <summary>Filtrar por rol exacto: Admin, BusinessOwner, User.</summary>
    public string? Role { get; init; }

    /// <summary>Filtrar por estado activo/inactivo.</summary>
    public bool? IsActive { get; init; }
}
