using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[PrimaryKey("BranchId", "ServiceId")]
[Table("branch_service", Schema = "app")]
public partial class BranchService
{
    [Key]
    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Key]
    [Column("service_id")]
    public Guid ServiceId { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("active")]
    public bool Active { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("BranchServices")]
    public virtual Branch Branch { get; set; } = null!;

    [ForeignKey("ServiceId")]
    [InverseProperty("BranchServices")]
    public virtual Service Service { get; set; } = null!;

    [InverseProperty("BranchService")]
    public virtual ServicePrice? ServicePrice { get; set; }
}
