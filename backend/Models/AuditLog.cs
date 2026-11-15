using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("audit_log", Schema = "app")]
public partial class AuditLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("entity_schema")]
    [StringLength(63)]
    public string EntitySchema { get; set; } = null!;

    [Column("entity_table")]
    [StringLength(63)]
    public string EntityTable { get; set; } = null!;

    [Column("operation")]
    [MaxLength(1)]
    public char Operation { get; set; }

    [Column("created_by_user_id")]
    public Guid CreatedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("transaction_id", TypeName = "xid8")]
    public ulong TransactionId { get; set; }

    [Column("old_value", TypeName = "jsonb")]
    public string? OldValue { get; set; }

    [Column("new_value", TypeName = "jsonb")]
    public string? NewValue { get; set; }
}
