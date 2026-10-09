using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS_MT.Interfaces;
using POS_MT.Services;

namespace POS_MT.Controllers;

[Authorize]
public class NavController : Controller
{
    private readonly INavContextService _navContext;

    public NavController(INavContextService navContext)
    {
        _navContext = navContext;
    }

    [HttpGet]
    public IActionResult Select(string key)
    {
        var area = NavigationCatalog.FindArea(key);
        if (area is null)
        {
            _navContext.Clear();
            return RedirectToAction("Index", "Dashboard");
        }

        _navContext.SetArea(area.Key);

        if (string.Equals(area.Key, "POS", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("Index", "POS");
        }

        if (string.Equals(area.Key, "Customers", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("Index", "Customers");
        }

        if (string.Equals(area.Key, "Inventories", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("Index", "Inventories");
        }

        if (string.Equals(area.Key, "AllModules", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("Index", "Modules");
        }

        if (string.Equals(area.Key, "Reports", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("TotalSale", "Reports");
        }

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult Clear()
    {
        _navContext.Clear();
        return RedirectToAction("Index", "Dashboard");
    }
}
