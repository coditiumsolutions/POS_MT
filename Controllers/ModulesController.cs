using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS_MT.Interfaces;
using POS_MT.Services;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize(Policy = "Staff")]
public class ModulesController : Controller
{
    private readonly INavContextService _navContext;

    public ModulesController(INavContextService navContext)
    {
        _navContext = navContext;
    }

    [HttpGet]
    public IActionResult Index()
    {
        _navContext.SetArea("AllModules");
        var model = new ModuleCardsViewModel
        {
            PageTitle = "Modules",
            PageSubtitle = "Open a module to manage your POS operations.",
            Sections = RoleModuleAccess.FilterSections(NavigationCatalog.MainModuleSections, User).ToList(),
            Modules = RoleModuleAccess.Filter(NavigationCatalog.MainModules, User).ToList()
        };

        return View(model);
    }
}
