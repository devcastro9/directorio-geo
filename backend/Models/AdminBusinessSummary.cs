using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Keyless]
public partial class AdminBusinessSummary
{
    [Column("id")]
    public Guid? Id { get; set; }

    [Column("trade_name")]
    [StringLength(200)]
    public string? TradeName { get; set; }

    [Column("publication_status")]
    [StringLength(20)]
    public string? PublicationStatus { get; set; }

    [Column("status")]
    [StringLength(30)]
    public string? Status { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("active_branches")]
    public long? ActiveBranches { get; set; }

    [Column("owners")]
    public long? Owners { get; set; }

    [Column("reviews")]
    public long? Reviews { get; set; }

    [Column("ads")]
    public long? Ads { get; set; }
}
