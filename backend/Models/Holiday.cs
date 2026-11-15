using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("holiday", Schema = "catalog")]
[Index("HolidayDate", "TerritoryId", "Name", Name = "holiday_holiday_date_territory_id_name_key", IsUnique = true)]
public partial class Holiday
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    [StringLength(150)]
    public string Name { get; set; } = null!;

    [Column("holiday_date")]
    public DateOnly HolidayDate { get; set; }

    [Column("territory_id")]
    public Guid? TerritoryId { get; set; }

    [Column("recurring_annually")]
    public bool RecurringAnnually { get; set; }

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

    [ForeignKey("TerritoryId")]
    [InverseProperty("Holidays")]
    public virtual Territory? Territory { get; set; }
}
