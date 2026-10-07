using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS_MT.Services;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize]
public class SetupController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var model = new ModuleCardsViewModel
        {
            PageTitle = "Setup",
            PageSubtitle = "Configure system settings and master data.",
            Modules = NavigationCatalog.SetupModules.ToList()
        };

        return View("~/Views/Modules/Index.cshtml", model);
    }
}
