using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[Table("event", Schema = "app")]
[Index("EventType", "OccurredAt", Name = "event_type_time")]
public partial class Event
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("event_type")]
    [StringLength(50)]
    public string EventType { get; set; } = null!;

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    [Column("user_id")]
    [StringLength(200)]
    public string? UserId { get; set; }

    [Column("session_key")]
    [StringLength(128)]
    public string? SessionKey { get; set; }

    [Column("ip_address")]
    public IPAddress IpAddress { get; set; } = null!;

    [Column("user_agent")]
    [StringLength(1000)]
    public string UserAgent { get; set; } = null!;

    [Column("business_id")]
    public Guid? BusinessId { get; set; }

    [Column("branch_id")]
    public Guid? BranchId { get; set; }

    [Column("category_id")]
    public Guid? CategoryId { get; set; }

    [Column("territory_id")]
    public Guid? TerritoryId { get; set; }

    [Column("query_text")]
    public string? QueryText { get; set; }

    [Column("source_ref")]
    public string? SourceRef { get; set; }

    [Column("metadata", TypeName = "jsonb")]
    public string Metadata { get; set; } = null!;
}
