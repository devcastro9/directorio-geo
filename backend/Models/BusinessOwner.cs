using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[Table("business_owner", Schema = "app")]
[Index("BusinessId", "UserId", Name = "business_owner_business_id_user_id_key", IsUnique = true)]
public partial class BusinessOwner
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("business_id")]
    public Guid BusinessId { get; set; }

    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Column("verified_at")]
    public DateTime? VerifiedAt { get; set; }

    [Column("verified_by_user_id")]
    public string? VerifiedByUserId { get; set; }

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("BusinessId")]
    [InverseProperty("BusinessOwners")]
    public virtual Business Business { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("BusinessOwners")]
    public virtual OwnerIdentity User { get; set; } = null!;
}
