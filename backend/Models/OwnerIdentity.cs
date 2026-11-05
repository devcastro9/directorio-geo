using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[Table("owner_identity", Schema = "app")]
public partial class OwnerIdentity
{
    [Key]
    [Column("user_id")]
    [StringLength(200)]
    public string UserId { get; set; } = null!;

    [Column("verified_at")]
    public DateTime? VerifiedAt { get; set; }

    [Column("verification_method")]
    [StringLength(30)]
    public string? VerificationMethod { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    [Column("verified_by_user_id")]
    [StringLength(200)]
    public string? VerifiedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<BusinessOwner> BusinessOwners { get; set; } = new List<BusinessOwner>();
}
