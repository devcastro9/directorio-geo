using System;

namespace backend.DTOs.Businesses;

public sealed record CatalogRefDto(Guid Id, string Code, string Name);
