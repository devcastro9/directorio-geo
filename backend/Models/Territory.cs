using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace backend.Models;

[Table("territory", Schema = "geo")]
[Index("OsmType", "OsmId", Name = "territory_osm_type_osm_id_key", IsUnique = true)]
[Index("ParentId", Name = "territory_parent_idx")]
[Index("TerritoryType", "CodeIne", Name = "territory_territory_type_code_ine_key", IsUnique = true)]
public partial class Territory
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("parent_id")]
    public Guid? ParentId { get; set; }

    [Column("territory_type")]
    public string TerritoryType { get; set; } = null!;

    [Column("code_ine")]
    public string? CodeIne { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("source")]
    public string Source { get; set; } = null!;

    [Column("osm_type")]
    public string? OsmType { get; set; }

    [Column("osm_id")]
    public long? OsmId { get; set; }

    [Column("source_version")]
    public string? SourceVersion { get; set; }

    [Column("boundary", TypeName = "geography(MultiPolygon,4326)")]
    public MultiPolygon? Boundary { get; set; }

    [Column("centroid", TypeName = "geography(Point,4326)")]
    public Point? Centroid { get; set; }

    [Column("active")]
    public bool Active { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Territory")]
    public virtual ICollection<Branch> Branches { get; set; } = new List<Branch>();

    [InverseProperty("Territory")]
    public virtual ICollection<Holiday> Holidays { get; set; } = new List<Holiday>();

    [InverseProperty("Parent")]
    public virtual ICollection<Territory> InverseParent { get; set; } = new List<Territory>();

    [ForeignKey("ParentId")]
    [InverseProperty("InverseParent")]
    public virtual Territory? Parent { get; set; }
}
