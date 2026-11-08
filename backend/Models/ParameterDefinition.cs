using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("parameter_definition", Schema = "catalog")]
public partial class ParameterDefinition
{
    [Key]
    [Column("code")]
    [StringLength(100)]
    public string Code { get; set; } = null!;

    [Column("description")]
    public string Description { get; set; } = null!;

    [Column("data_type")]
    [StringLength(20)]
    public string DataType { get; set; } = null!;

    [Column("default_value", TypeName = "jsonb")]
    public string DefaultValue { get; set; } = null!;

    [Column("min_number")]
    public decimal? MinNumber { get; set; }

    [Column("max_number")]
    public decimal? MaxNumber { get; set; }

    [Column("allowed_values", TypeName = "jsonb")]
    public string? AllowedValues { get; set; }

    [Column("active")]
    public bool Active { get; set; }

    [InverseProperty("CodeNavigation")]
    public virtual ICollection<ParameterValue> ParameterValues { get; set; } = new List<ParameterValue>();
}
