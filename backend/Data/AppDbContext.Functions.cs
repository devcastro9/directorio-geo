using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System;

namespace backend.Data;

/// <summary>
/// Parte parcial de <see cref="AppDbContext"/> que mapea funciones escalares de PostgreSQL
/// a métodos estáticos de C#, para poder usarlas dentro de consultas LINQ.
/// </summary>
/// <remarks>
/// Los métodos estáticos son solo "stubs": EF Core los reconoce en el árbol de expresiones
/// y los traduce a llamadas SQL. Fuera de una consulta a la base de datos lanzan
/// <see cref="NotSupportedException"/>.
/// Esquemas involucrados: <c>app</c> (lógica de negocio), <c>geo</c> (PostGIS) y
/// <c>catalog</c> (parámetros de configuración).
/// </remarks>
public partial class AppDbContext
{
    public static bool IsOpenAt(Guid branchId, DateTime whenUtc)
        => throw new NotSupportedException();

    public static bool IsHoliday(Guid branchId, DateOnly day)
        => throw new NotSupportedException();

    public static bool IsBusinessOwner(Guid userId, Guid businessId)
        => throw new NotSupportedException();

    public static string Normalize(string? text)
        => throw new NotSupportedException();

    public static Guid? ResolveTerritory(Geometry point)
        => throw new NotSupportedException();

    public static int ParamInt(string code)
        => throw new NotSupportedException();

    public static bool ParamBool(string code)
        => throw new NotSupportedException();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(IsOpenAt), [typeof(Guid), typeof(DateTime)])!)
            .HasSchema("app").HasName("is_open_at")
            .HasParameter("branchId", p => p.HasStoreType("uuid"))
            .HasParameter("whenUtc", p => p.HasStoreType("timestamptz"));

        modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(IsHoliday), [typeof(Guid), typeof(DateOnly)])!)
            .HasSchema("app").HasName("is_holiday")
            .HasParameter("branchId", p => p.HasStoreType("uuid"))
            .HasParameter("day", p => p.HasStoreType("date"));

        modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(IsBusinessOwner), [typeof(Guid), typeof(Guid)])!)
            .HasSchema("app").HasName("is_business_owner");

        modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(Normalize), [typeof(string)])!)
            .HasSchema("app").HasName("normalize");

        modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(ResolveTerritory), [typeof(Geometry)])!)
            .HasSchema("geo").HasName("resolve_territory")
            .HasParameter("point", p => p.HasStoreType("geography"));

        modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(ParamInt), [typeof(string)])!)
            .HasSchema("catalog").HasName("param_int");

        modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(ParamBool), [typeof(string)])!)
            .HasSchema("catalog").HasName("param_bool");
    }
}