using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("review_reply", Schema = "app")]
[Index("ReviewId", Name = "review_reply_review_id_key", IsUnique = true)]
public partial class ReviewReply
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("review_id")]
    public Guid ReviewId { get; set; }

    [Column("owner_user_id")]
    public Guid OwnerUserId { get; set; }

    [Column("reply_text")]
    public string ReplyText { get; set; } = null!;

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("ReviewId")]
    [InverseProperty("ReviewReply")]
    public virtual Review Review { get; set; } = null!;
}
