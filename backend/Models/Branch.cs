using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace backend.Models;

[Table("branch", Schema = "app")]
[Index("BusinessId", "Code", Name = "branch_business_id_code_key", IsUnique = true)]
[Index("Slug", Name = "branch_slug_key", IsUnique = true)]
[Index("Source", "SourceKey", Name = "branch_source_source_key_key", IsUnique = true)]
public partial class Branch
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("business_id")]
    public Guid BusinessId { get; set; }

    [Column("code")]
    [StringLength(50)]
    public string? Code { get; set; }

    [Column("slug")]
    [StringLength(160)]
    public string? Slug { get; set; }

    [Column("name")]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    [Column("address")]
    public string Address { get; set; } = null!;

    [Column("territory_id")]
    public Guid? TerritoryId { get; set; }

    [Column("location", TypeName = "geography(Point,4326)")]
    public Point Location { get; set; } = null!;

    [Column("location_source")]
    [StringLength(20)]
    public string? LocationSource { get; set; }

    [Column("status")]
    [StringLength(30)]
    public string Status { get; set; } = null!;

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("price_level")]
    public short? PriceLevel { get; set; }

    [Column("min_price_bob")]
    [Precision(14, 2)]
    public decimal? MinPriceBob { get; set; }

    [Column("max_price_bob")]
    [Precision(14, 2)]
    public decimal? MaxPriceBob { get; set; }

    [Column("emergency")]
    public bool Emergency { get; set; }

    [Column("home_service")]
    public bool HomeService { get; set; }

    [Column("accessibility", TypeName = "jsonb")]
    public string Accessibility { get; set; } = null!;

    [Column("source")]
    [StringLength(30)]
    public string Source { get; set; } = null!;

    [Column("source_key")]
    [StringLength(200)]
    public string? SourceKey { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by_user_id")]
    public Guid? DeletedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Branch")]
    public virtual ICollection<AdPlacement> AdPlacements { get; set; } = new List<AdPlacement>();

    [InverseProperty("Branch")]
    public virtual ICollection<BranchCategory> BranchCategories { get; set; } = new List<BranchCategory>();

    [InverseProperty("Branch")]
    public virtual ICollection<BranchContact> BranchContacts { get; set; } = new List<BranchContact>();

    [InverseProperty("Branch")]
    public virtual ICollection<BranchFeature> BranchFeatures { get; set; } = new List<BranchFeature>();

    [InverseProperty("Branch")]
    public virtual ICollection<BranchHourOverride> BranchHourOverrides { get; set; } = new List<BranchHourOverride>();

    [InverseProperty("Branch")]
    public virtual ICollection<BranchPayment> BranchPayments { get; set; } = new List<BranchPayment>();

    [InverseProperty("Branch")]
    public virtual BranchPhoto? BranchPhoto { get; set; }

    [InverseProperty("Branch")]
    public virtual ICollection<BranchService> BranchServices { get; set; } = new List<BranchService>();

    [ForeignKey("BusinessId")]
    [InverseProperty("Branch")]
    public virtual Business Business { get; set; } = null!;

    [InverseProperty("Branch")]
    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

    [InverseProperty("MatchedBranch")]
    public virtual ICollection<ImportRow> ImportRows { get; set; } = new List<ImportRow>();

    [InverseProperty("Branch")]
    public virtual ICollection<OpeningHour> OpeningHours { get; set; } = new List<OpeningHour>();

    [InverseProperty("Branch")]
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    [ForeignKey("TerritoryId")]
    [InverseProperty("Branches")]
    public virtual Territory? Territory { get; set; }
}
