using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[PrimaryKey("BranchId", "CategoryId")]
[Table("branch_category", Schema = "app")]
public partial class BranchCategory
{
    [Key]
    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Key]
    [Column("category_id")]
    public Guid CategoryId { get; set; }

    [Column("active")]
    public bool Active { get; set; }

    [Column("created_by_user_id")]
    public Guid? CreatedByUserId { get; set; }

    [Column("updated_by_user_id")]
    public Guid? UpdatedByUserId { get; set; }

    [Column("deleted_by_user_id")]
    public Guid? DeletedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("BranchCategories")]
    public virtual Branch Branch { get; set; } = null!;

    [ForeignKey("CategoryId")]
    [InverseProperty("BranchCategories")]
    public virtual Category Category { get; set; } = null!;
}
