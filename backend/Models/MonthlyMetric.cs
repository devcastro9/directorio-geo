using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[Keyless]
[Table("monthly_metric", Schema = "app")]
[Index("MetricMonth", "EventType", "BusinessId", "BranchId", Name = "monthly_metric_metric_month_event_type_business_id_branch_i_key", IsUnique = true)]
public partial class MonthlyMetric
{
    [Column("metric_month")]
    public DateOnly MetricMonth { get; set; }

    [Column("event_type")]
    public string EventType { get; set; } = null!;

    [Column("business_id")]
    public Guid? BusinessId { get; set; }

    [Column("branch_id")]
    public Guid? BranchId { get; set; }

    [Column("total_count")]
    public long TotalCount { get; set; }
}
