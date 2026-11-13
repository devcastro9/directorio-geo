namespace backend.DTOs;

/// <summary>Envuelve los datos de una operación correcta.</summary>
public sealed record ApiResponse<T>(T Data);
