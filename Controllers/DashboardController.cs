using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS_MT.Interfaces;

namespace POS_MT.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index(int? itemMonth, CancellationToken cancellationToken)
    {
        var model = await _dashboardService.GetSnapshotAsync(itemMonth, cancellationToken);
        return View(model);
    }
}
