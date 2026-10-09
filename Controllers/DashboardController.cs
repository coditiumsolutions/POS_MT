using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS_MT.Interfaces;

namespace POS_MT.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;
    private readonly INavContextService _navContext;

    public DashboardController(IDashboardService dashboardService, INavContextService navContext)
    {
        _dashboardService = dashboardService;
        _navContext = navContext;
    }

    private void EnsureDashboardNav() => _navContext.SetArea("Dashboard");

    [HttpGet]
    public async Task<IActionResult> Index(int? itemMonth, CancellationToken cancellationToken)
    {
        EnsureDashboardNav();
        ViewData["Title"] = "Bar Graphs";
        var model = await _dashboardService.GetSnapshotAsync(itemMonth ?? 6, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> PieGraphs(int? itemMonth, CancellationToken cancellationToken)
    {
        EnsureDashboardNav();
        ViewData["Title"] = "Pie Graphs";
        var model = await _dashboardService.GetSnapshotAsync(itemMonth ?? 6, cancellationToken);
        return View(model);
    }
}
