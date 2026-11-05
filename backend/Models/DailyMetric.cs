using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[Keyless]
[Table("daily_metric", Schema = "app")]
[Index("MetricDate", "EventType", "BusinessId", "BranchId", Name = "daily_metric_metric_date_event_type_business_id_branch_id_key", IsUnique = true)]
public partial class DailyMetric
{
    [Column("metric_date")]
    public DateOnly MetricDate { get; set; }

    [Column("event_type")]
    public string EventType { get; set; } = null!;

    [Column("business_id")]
    public Guid? BusinessId { get; set; }

    [Column("branch_id")]
    public Guid? BranchId { get; set; }

    [Column("total_count")]
    public long TotalCount { get; set; }
}
