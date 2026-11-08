using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[PrimaryKey("UserId", "BranchId")]
[Table("favorite", Schema = "app")]
public partial class Favorite
{
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Key]
    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("Favorites")]
    public virtual Branch Branch { get; set; } = null!;
}
