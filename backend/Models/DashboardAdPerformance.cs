using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Keyless]
public partial class DashboardAdPerformance
{
    [Column("ad_id")]
    public Guid? AdId { get; set; }

    [Column("business_id")]
    public Guid? BusinessId { get; set; }

    [Column("branch_id")]
    public Guid? BranchId { get; set; }

    [Column("slot")]
    [StringLength(30)]
    public string? Slot { get; set; }

    [Column("amount_bob")]
    [Precision(14, 2)]
    public decimal? AmountBob { get; set; }

    [Column("starts_at")]
    public DateTime? StartsAt { get; set; }

    [Column("ends_at")]
    public DateTime? EndsAt { get; set; }

    [Column("impressions")]
    public long? Impressions { get; set; }

    [Column("clicks")]
    public long? Clicks { get; set; }
}
