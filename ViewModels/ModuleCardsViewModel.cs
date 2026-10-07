using POS_MT.Services;

namespace POS_MT.ViewModels;

public sealed class ModuleCardsViewModel
{
    public string PageTitle { get; set; } = "Modules";

    public string PageSubtitle { get; set; } = string.Empty;

    public List<ModuleCardItem> Modules { get; set; } = [];
}
