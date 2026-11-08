using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[PrimaryKey("BranchId", "MethodId")]
[Table("branch_payment", Schema = "app")]
public partial class BranchPayment
{
    [Key]
    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Key]
    [Column("method_id")]
    public Guid MethodId { get; set; }

    [Column("active")]
    public bool Active { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("BranchPayments")]
    public virtual Branch Branch { get; set; } = null!;

    [ForeignKey("MethodId")]
    [InverseProperty("BranchPayments")]
    public virtual PaymentMethod Method { get; set; } = null!;
}
