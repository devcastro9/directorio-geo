using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[Table("import_row", Schema = "app")]
[Index("BatchId", "RowNumber", Name = "import_row_batch_id_row_number_key", IsUnique = true)]
[Index("BatchId", "Decision", Name = "import_row_decision_idx")]
public partial class ImportRow
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("batch_id")]
    public Guid BatchId { get; set; }

    [Column("row_number")]
    public int RowNumber { get; set; }

    [Column("payload", TypeName = "jsonb")]
    public string Payload { get; set; } = null!;

    [Column("normalized_name")]
    [StringLength(200)]
    public string? NormalizedName { get; set; }

    [Column("matched_business_id")]
    public Guid? MatchedBusinessId { get; set; }

    [Column("matched_branch_id")]
    public Guid? MatchedBranchId { get; set; }

    [Column("decision")]
    [StringLength(20)]
    public string Decision { get; set; } = null!;

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("reviewed_by_user_id")]
    public Guid? ReviewedByUserId { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [ForeignKey("BatchId")]
    [InverseProperty("ImportRows")]
    public virtual ImportBatch Batch { get; set; } = null!;

    [ForeignKey("MatchedBranchId")]
    [InverseProperty("ImportRows")]
    public virtual Branch? MatchedBranch { get; set; }

    [ForeignKey("MatchedBusinessId")]
    [InverseProperty("ImportRows")]
    public virtual Business? MatchedBusiness { get; set; }
}
