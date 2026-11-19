using System;
using System.Collections.Generic;

namespace backend.DTOs.Catalogs;

public sealed record CatalogItemDto(Guid Id, string Code, string Name, int SortOrder);

public sealed record CatalogsDto(
    IReadOnlyList<CatalogItemDto> Services,
    IReadOnlyList<CatalogItemDto> Features,
    IReadOnlyList<CatalogItemDto> PaymentMethods
);
