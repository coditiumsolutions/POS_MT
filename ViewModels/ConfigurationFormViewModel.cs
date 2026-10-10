using System.ComponentModel.DataAnnotations;

namespace POS_MT.ViewModels;

public sealed class ConfigurationFormViewModel
{
    public int Uid { get; set; }

    [Display(Name = "Config Id")]
    public int? ConfigId { get; set; }

    [Required, StringLength(50), Display(Name = "Config Key")]
    public string ConfigKey { get; set; } = string.Empty;

    [Display(Name = "Config Value")]
    public string? ConfigValue { get; set; }
}
