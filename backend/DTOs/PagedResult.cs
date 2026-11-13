using System.Collections.Generic;

namespace backend.DTOs;

/// <summary>Representa una página y el total de resultados del filtro.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
