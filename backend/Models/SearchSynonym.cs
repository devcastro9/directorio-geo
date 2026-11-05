using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace backend.Models;

[Table("search_synonym", Schema = "app")]
[Index("Term", "Synonym", "Locale", Name = "search_synonym_term_synonym_locale_key", IsUnique = true)]
public partial class SearchSynonym
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("term")]
    public string Term { get; set; } = null!;

    [Column("synonym")]
    public string Synonym { get; set; } = null!;

    [Column("locale")]
    public string Locale { get; set; } = null!;

    [Column("active")]
    public bool Active { get; set; }
}
