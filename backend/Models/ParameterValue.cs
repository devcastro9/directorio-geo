using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("parameter_value", Schema = "catalog")]
public partial class ParameterValue
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("code")]
    [StringLength(100)]
    public string Code { get; set; } = null!;

    [Column("scope_type")]
    [StringLength(20)]
    public string ScopeType { get; set; } = null!;

    [Column("scope_id")]
    public Guid? ScopeId { get; set; }

    [Column("value", TypeName = "jsonb")]
    public string Value { get; set; } = null!;

    [Column("valid_from")]
    public DateTime ValidFrom { get; set; }

    [Column("valid_to")]
    public DateTime? ValidTo { get; set; }

    [Column("active")]
    public bool Active { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("Code")]
    [InverseProperty("ParameterValues")]
    public virtual ParameterDefinition CodeNavigation { get; set; } = null!;
}
