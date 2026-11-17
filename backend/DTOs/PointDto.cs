using NetTopologySuite.Geometries;

namespace backend.DTOs;

/// <summary>Punto GeoJSON con coordenadas [longitud, latitud] SRID 4326.</summary>
public sealed record PointDto(string Type, double[] Coordinates)
{
    public static PointDto FromPoint(Point p) => new("Point", [p.X, p.Y]);
}
