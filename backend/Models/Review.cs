using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("review", Schema = "app")]
public partial class Review
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("rating")]
    public short Rating { get; set; }

    [Column("title")]
    [StringLength(200)]
    public string? Title { get; set; }

    [Column("body")]
    public string? Body { get; set; }

    [Column("moderation_status")]
    [StringLength(20)]
    public string ModerationStatus { get; set; } = null!;

    [Column("moderation_reason")]
    public string? ModerationReason { get; set; }

    [Column("moderated_at")]
    public DateTime? ModeratedAt { get; set; }

    [Column("moderated_by_user_id")]
    public Guid? ModeratedByUserId { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("Reviews")]
    public virtual Branch Branch { get; set; } = null!;

    [InverseProperty("Review")]
    public virtual ReviewReply? ReviewReply { get; set; }
}
