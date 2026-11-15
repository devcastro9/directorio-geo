using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Keyless]
public partial class PublishedBranch
{
    [Column("branch_id")]
    public Guid? BranchId { get; set; }

    [Column("business_id")]
    public Guid? BusinessId { get; set; }

    [Column("trade_name")]
    [StringLength(200)]
    public string? TradeName { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("branch_name")]
    [StringLength(200)]
    public string? BranchName { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("territory_id")]
    public Guid? TerritoryId { get; set; }

    [Column("location", TypeName = "geography(Point,4326)")]
    public Point? Location { get; set; }

    [Column("branch_status")]
    [StringLength(30)]
    public string? BranchStatus { get; set; }

    [Column("business_status")]
    [StringLength(20)]
    public string? BusinessStatus { get; set; }
}
