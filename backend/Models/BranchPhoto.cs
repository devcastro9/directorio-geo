using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("branch_photo", Schema = "app")]
public partial class BranchPhoto
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("photo_data")]
    public byte[] PhotoData { get; set; } = null!;

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

    [Column("created_by_user_id")]
    public Guid CreatedByUserId { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("BranchPhotos")]
    public virtual Branch Branch { get; set; } = null!;
}
