using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS_MT.Services;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize]
public class ModulesController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var model = new ModuleCardsViewModel
        {
            PageTitle = "Modules",
            PageSubtitle = "Open a module to manage your POS operations.",
            Modules = NavigationCatalog.MainModules.ToList()
        };

        return View(model);
    }
}
