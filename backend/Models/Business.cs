using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[Table("business", Schema = "app")]
[Index("Slug", Name = "business_slug_key", IsUnique = true)]
[Index("Source", "SourceKey", Name = "business_source_source_key_key", IsUnique = true)]
[Index("TaxId", Name = "business_tax_id_key", IsUnique = true)]
public partial class Business
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("slug")]
    [StringLength(160)]
    public string? Slug { get; set; }

    [Column("legal_name")]
    [StringLength(250)]
    public string? LegalName { get; set; }

    [Column("trade_name")]
    [StringLength(200)]
    public string TradeName { get; set; } = null!;

    [Column("tax_id")]
    [StringLength(50)]
    public string? TaxId { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("status")]
    [StringLength(30)]
    public string Status { get; set; } = null!;

    [Column("publication_status")]
    [StringLength(20)]
    public string PublicationStatus { get; set; } = null!;

    [Column("price_level")]
    public short? PriceLevel { get; set; }

    [Column("source")]
    [StringLength(30)]
    public string Source { get; set; } = null!;

    [Column("source_key")]
    [StringLength(200)]
    public string? SourceKey { get; set; }

    [Column("created_by_user_id")]
    public Guid? CreatedByUserId { get; set; }

    [Column("updated_by_user_id")]
    public Guid? UpdatedByUserId { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by_user_id")]
    public Guid? DeletedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Business")]
    public virtual ICollection<AdPlacement> AdPlacements { get; set; } = new List<AdPlacement>();

    [InverseProperty("Business")]
    public virtual Branch? Branch { get; set; }

    [InverseProperty("Business")]
    public virtual ICollection<BusinessOwner> BusinessOwners { get; set; } = new List<BusinessOwner>();

    [InverseProperty("MatchedBusiness")]
    public virtual ICollection<ImportRow> ImportRows { get; set; } = new List<ImportRow>();
}
