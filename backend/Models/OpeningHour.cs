using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("opening_hour", Schema = "app")]
public partial class OpeningHour
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("weekday")]
    public short Weekday { get; set; }

    [Column("opens_at")]
    public TimeOnly OpensAt { get; set; }

    [Column("closes_at")]
    public TimeOnly ClosesAt { get; set; }

    [Column("active")]
    public bool Active { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("OpeningHours")]
    public virtual Branch Branch { get; set; } = null!;
}
