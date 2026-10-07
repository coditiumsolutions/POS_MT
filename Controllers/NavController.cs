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
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult Clear()
    {
        _navContext.Clear();
        return RedirectToAction("Index", "Dashboard");
    }
}
