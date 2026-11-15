using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("business_subscription", Schema = "app")]
[Index("SubscriptionTypeId", Name = "subscription_type_idx")]
public partial class BusinessSubscription
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("business_id")]
    public Guid BusinessId { get; set; }

    [Column("subscription_type_id")]
    public Guid SubscriptionTypeId { get; set; }

    [Column("starts_at")]
    public DateTime StartsAt { get; set; }

    [Column("ends_at")]
    public DateTime EndsAt { get; set; }

    [Column("amount_bob")]
    [Precision(14, 2)]
    public decimal AmountBob { get; set; }

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

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

    [ForeignKey("BusinessId")]
    [InverseProperty("BusinessSubscriptions")]
    public virtual Business Business { get; set; } = null!;

    [ForeignKey("Status")]
    [InverseProperty("BusinessSubscriptions")]
    public virtual SubscriptionStatus StatusNavigation { get; set; } = null!;

    [InverseProperty("Subscription")]
    public virtual ICollection<SubscriptionPayment> SubscriptionPayments { get; set; } = new List<SubscriptionPayment>();

    [ForeignKey("SubscriptionTypeId")]
    [InverseProperty("BusinessSubscriptions")]
    public virtual SubscriptionType SubscriptionType { get; set; } = null!;
}
