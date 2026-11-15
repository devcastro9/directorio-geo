using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("subscription_payment", Schema = "app")]
[Index("PaymentMethodId", Name = "subscription_payment_method_idx")]
[Index("SubscriptionId", "CreatedAt", Name = "subscription_payment_subscription_idx", IsDescending = new[] { false, true })]
public partial class SubscriptionPayment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("subscription_id")]
    public Guid SubscriptionId { get; set; }

    [Column("payment_method_id")]
    public Guid? PaymentMethodId { get; set; }

    [Column("amount_bob")]
    [Precision(14, 2)]
    public decimal AmountBob { get; set; }

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Column("payment_reference")]
    public string? PaymentReference { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

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

    [ForeignKey("PaymentMethodId")]
    [InverseProperty("SubscriptionPayments")]
    public virtual PaymentMethod? PaymentMethod { get; set; }

    [ForeignKey("Status")]
    [InverseProperty("SubscriptionPayments")]
    public virtual SubscriptionPaymentStatus StatusNavigation { get; set; } = null!;

    [ForeignKey("SubscriptionId")]
    [InverseProperty("SubscriptionPayments")]
    public virtual BusinessSubscription Subscription { get; set; } = null!;
}
