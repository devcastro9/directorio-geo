using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[PrimaryKey("BranchId", "ServiceProductId")]
[Table("branch_service_product", Schema = "app")]
public partial class BranchServiceProduct
{
    [Key]
    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Key]
    [Column("service_product_id")]
    public Guid ServiceProductId { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("active")]
    public bool Active { get; set; }

    [Column("created_by_user_id")]
    public Guid? CreatedByUserId { get; set; }

    [Column("updated_by_user_id")]
    public Guid? UpdatedByUserId { get; set; }

    [Column("deleted_by_user_id")]
    public Guid? DeletedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("BranchServiceProducts")]
    public virtual Branch Branch { get; set; } = null!;

    [ForeignKey("ServiceProductId")]
    [InverseProperty("BranchServiceProducts")]
    public virtual ServiceProduct ServiceProduct { get; set; } = null!;

    [InverseProperty("BranchServiceProduct")]
    public virtual ServiceProductPrice? ServiceProductPrice { get; set; }
}
