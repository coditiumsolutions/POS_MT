using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS_MT.Models;

[Table("Configuration")]
public partial class Configuration
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    public int? ConfigId { get; set; }

    [StringLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string? ConfigKey { get; set; }

    [Column(TypeName = "varchar(max)")]
    public string? ConfigValue { get; set; }
}
