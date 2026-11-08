using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Keyless]
public partial class DashboardBranchMetric
{
    [Column("branch_id")]
    public Guid? BranchId { get; set; }

    [Column("business_id")]
    public Guid? BusinessId { get; set; }

    [Column("views")]
    public decimal? Views { get; set; }

    [Column("contact_clicks")]
    public decimal? ContactClicks { get; set; }

    [Column("directions_clicks")]
    public decimal? DirectionsClicks { get; set; }

    [Column("ad_impressions")]
    public decimal? AdImpressions { get; set; }

    [Column("ad_clicks")]
    public decimal? AdClicks { get; set; }

    [Column("favorites")]
    public long? Favorites { get; set; }

    [Column("avg_rating")]
    public decimal? AvgRating { get; set; }
}
