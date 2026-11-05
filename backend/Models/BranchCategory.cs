using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

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

    [ForeignKey("BranchId")]
    [InverseProperty("BranchCategories")]
    public virtual Branch Branch { get; set; } = null!;

    [ForeignKey("CategoryId")]
    [InverseProperty("BranchCategories")]
    public virtual Category Category { get; set; } = null!;
}
