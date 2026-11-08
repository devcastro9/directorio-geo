using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[PrimaryKey("BranchId", "FeatureId")]
[Table("branch_feature", Schema = "app")]
public partial class BranchFeature
{
    [Key]
    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Key]
    [Column("feature_id")]
    public Guid FeatureId { get; set; }

    [Column("active")]
    public bool Active { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("BranchFeatures")]
    public virtual Branch Branch { get; set; } = null!;

    [ForeignKey("FeatureId")]
    [InverseProperty("BranchFeatures")]
    public virtual Feature Feature { get; set; } = null!;
}
