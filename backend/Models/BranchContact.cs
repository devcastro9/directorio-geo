using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[Table("branch_contact", Schema = "app")]
public partial class BranchContact
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("contact_type")]
    public string ContactType { get; set; } = null!;

    [Column("contact_value")]
    public string ContactValue { get; set; } = null!;

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("BranchContacts")]
    public virtual Branch Branch { get; set; } = null!;
}
