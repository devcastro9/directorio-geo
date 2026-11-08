using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("service_price", Schema = "app")]
public partial class ServicePrice
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("service_id")]
    public Guid ServiceId { get; set; }

    [Column("amount_min_bob")]
    [Precision(14, 2)]
    public decimal AmountMinBob { get; set; }

    [Column("amount_max_bob")]
    [Precision(14, 2)]
    public decimal AmountMaxBob { get; set; }

    [Column("active")]
    public bool Active { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("BranchId, ServiceId")]
    [InverseProperty("ServicePrice")]
    public virtual BranchService BranchService { get; set; } = null!;
}
