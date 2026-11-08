using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("audit_log", Schema = "app")]
[Index("EntityTable", "EntityKey", "ChangedAt", Name = "audit_entity_idx", IsDescending = new[] { false, false, true })]
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

    [Column("entity_key")]
    [StringLength(200)]
    public string EntityKey { get; set; } = null!;

    [Column("operation")]
    [MaxLength(1)]
    public char Operation { get; set; }

    [Column("actor_user_id")]
    public Guid? ActorUserId { get; set; }

    [Column("changed_at")]
    public DateTime ChangedAt { get; set; }

    [Column("transaction_id", TypeName = "xid8")]
    public ulong TransactionId { get; set; }

    [Column("old_value", TypeName = "jsonb")]
    public string? OldValue { get; set; }

    [Column("new_value", TypeName = "jsonb")]
    public string? NewValue { get; set; }
}
