using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("search_synonym", Schema = "catalog")]
[Index("Term", "Synonym", "Locale", Name = "search_synonym_term_synonym_locale_key", IsUnique = true)]
public partial class SearchSynonym
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("term")]
    [StringLength(150)]
    public string Term { get; set; } = null!;

    [Column("synonym")]
    [StringLength(150)]
    public string Synonym { get; set; } = null!;

    [Column("locale")]
    [StringLength(10)]
    public string Locale { get; set; } = null!;

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
}
