using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[Table("ad_placement", Schema = "app")]
public partial class AdPlacement
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("business_id")]
    public Guid? BusinessId { get; set; }

    [Column("branch_id")]
    public Guid? BranchId { get; set; }

    [Column("slot")]
    [StringLength(30)]
    public string Slot { get; set; } = null!;

    [Column("starts_at")]
    public DateTime StartsAt { get; set; }

    [Column("ends_at")]
    public DateTime EndsAt { get; set; }

    [Column("amount_bob")]
    [Precision(14, 2)]
    public decimal AmountBob { get; set; }

    [Column("payment_status")]
    [StringLength(20)]
    public string PaymentStatus { get; set; } = null!;

    [Column("priority")]
    public int Priority { get; set; }

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Column("invoice_ref")]
    [StringLength(100)]
    public string? InvoiceRef { get; set; }

    [Column("created_by_user_id")]
    public Guid? CreatedByUserId { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("AdPlacements")]
    public virtual Branch? Branch { get; set; }

    [ForeignKey("BusinessId")]
    [InverseProperty("AdPlacements")]
    public virtual Business? Business { get; set; }
}
