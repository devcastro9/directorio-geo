using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[Table("payment_method", Schema = "catalog")]
[Index("Code", Name = "payment_method_code_key", IsUnique = true)]
public partial class PaymentMethod
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("code")]
    public string Code { get; set; } = null!;

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("active")]
    public bool Active { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Method")]
    public virtual ICollection<BranchPayment> BranchPayments { get; set; } = new List<BranchPayment>();
}
