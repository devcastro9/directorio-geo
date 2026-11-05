using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

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

    [Column("branch_price_level")]
    public short? BranchPriceLevel { get; set; }

    [Column("business_price_level")]
    public short? BusinessPriceLevel { get; set; }

    [Column("min_price_bob")]
    [Precision(14, 2)]
    public decimal? MinPriceBob { get; set; }

    [Column("max_price_bob")]
    [Precision(14, 2)]
    public decimal? MaxPriceBob { get; set; }

    [Column("emergency")]
    public bool? Emergency { get; set; }

    [Column("home_service")]
    public bool? HomeService { get; set; }
}
