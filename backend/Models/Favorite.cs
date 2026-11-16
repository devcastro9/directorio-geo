using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[PrimaryKey("UserId", "BranchId")]
[Table("favorite", Schema = "app")]
[Index("BranchId", Name = "favorite_branch_idx")]
public partial class Favorite
{
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Key]
    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("created_by_user_id")]
    public Guid? CreatedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("Favorites")]
    public virtual Branch Branch { get; set; } = null!;
}
