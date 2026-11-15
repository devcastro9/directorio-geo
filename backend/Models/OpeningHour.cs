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

    [Column("is_24_hours")]
    public bool Is24Hours { get; set; }

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

    [ForeignKey("BranchId")]
    [InverseProperty("OpeningHours")]
    public virtual Branch Branch { get; set; } = null!;
}
