using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("holiday", Schema = "app")]
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

    [ForeignKey("TerritoryId")]
    [InverseProperty("Holidays")]
    public virtual Territory? Territory { get; set; }
}
