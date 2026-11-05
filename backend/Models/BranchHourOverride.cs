using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[Table("branch_hour_override", Schema = "app")]
[Index("BranchId", "ServiceDate", Name = "branch_hour_override_branch_id_service_date_key", IsUnique = true)]
public partial class BranchHourOverride
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("service_date")]
    public DateOnly ServiceDate { get; set; }

    [Column("is_closed")]
    public bool IsClosed { get; set; }

    [Column("opens_at")]
    public TimeOnly? OpensAt { get; set; }

    [Column("closes_at")]
    public TimeOnly? ClosesAt { get; set; }

    [Column("reason")]
    public string? Reason { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("BranchHourOverrides")]
    public virtual Branch Branch { get; set; } = null!;
}
