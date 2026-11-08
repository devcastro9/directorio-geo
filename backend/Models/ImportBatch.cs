using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("import_batch", Schema = "app")]
public partial class ImportBatch
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("file_name")]
    [StringLength(255)]
    public string FileName { get; set; } = null!;

    [Column("file_format")]
    [StringLength(10)]
    public string FileFormat { get; set; } = null!;

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Column("created_by_user_id")]
    public Guid? CreatedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("total_rows")]
    public int TotalRows { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [InverseProperty("Batch")]
    public virtual ICollection<ImportRow> ImportRows { get; set; } = new List<ImportRow>();
}
