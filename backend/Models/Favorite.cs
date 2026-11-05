using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[PrimaryKey("UserId", "BranchId")]
[Table("favorite", Schema = "app")]
public partial class Favorite
{
    [Key]
    [Column("user_id")]
    [StringLength(200)]
    public string UserId { get; set; } = null!;

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
