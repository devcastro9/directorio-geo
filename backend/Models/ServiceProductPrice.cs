using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("service_product_price", Schema = "app")]
public partial class ServiceProductPrice
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("service_product_id")]
    public Guid ServiceProductId { get; set; }

    [Column("amount_min_bob")]
    [Precision(14, 2)]
    public decimal AmountMinBob { get; set; }

    [Column("amount_max_bob")]
    [Precision(14, 2)]
    public decimal AmountMaxBob { get; set; }

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
    public DateTime UpdatedAt { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [ForeignKey("BranchId, ServiceProductId")]
    [InverseProperty("ServiceProductPrice")]
    public virtual BranchServiceProduct BranchServiceProduct { get; set; } = null!;
}
