using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("business_photo", Schema = "app")]
[Index("PhotoSha256", Name = "business_photo_sha256_idx")]
public partial class BusinessPhoto
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("business_id")]
    public Guid BusinessId { get; set; }

    [Column("photo_data")]
    public byte[] PhotoData { get; set; } = null!;

    [Column("photo_sha256")]
    public byte[]? PhotoSha256 { get; set; }

    [Column("mime_type")]
    [StringLength(30)]
    public string MimeType { get; set; } = null!;

    [Column("file_size")]
    public long? FileSize { get; set; }

    [Column("filename")]
    [StringLength(255)]
    public string? Filename { get; set; }

    [Column("alt_text")]
    [StringLength(300)]
    public string? AltText { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("active")]
    public bool Active { get; set; }

    [Column("created_by_user_id")]
    public Guid CreatedByUserId { get; set; }

    [Column("updated_by_user_id")]
    public Guid? UpdatedByUserId { get; set; }

    [Column("deleted_by_user_id")]
    public Guid? DeletedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [ForeignKey("BusinessId")]
    [InverseProperty("BusinessPhotos")]
    public virtual Business Business { get; set; } = null!;
}
