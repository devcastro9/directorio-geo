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

    [Column("reply_text")]
    public string? ReplyText { get; set; }

    [Column("reply_by_user_id")]
    public Guid? ReplyByUserId { get; set; }

    [Column("replied_at")]
    public DateTime? RepliedAt { get; set; }

    [Column("created_by_user_id")]
    public Guid? CreatedByUserId { get; set; }

    [Column("updated_by_user_id")]
    public Guid? UpdatedByUserId { get; set; }

    [Column("deleted_by_user_id")]
    public Guid? DeletedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("moderated_at")]
    public DateTime? ModeratedAt { get; set; }

    [Column("moderated_by_user_id")]
    public Guid? ModeratedByUserId { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("Reviews")]
    public virtual Branch Branch { get; set; } = null!;

    [ForeignKey("ModerationStatus")]
    [InverseProperty("Reviews")]
    public virtual ReviewModerationStatus ModerationStatusNavigation { get; set; } = null!;
}
