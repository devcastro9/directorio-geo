using System.Collections.Generic;

namespace backend.DTOs.Auth;

public sealed record UserListDto(
    IReadOnlyList<UserDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);
