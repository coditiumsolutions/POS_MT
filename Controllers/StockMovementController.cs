using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS_MT.Interfaces;

namespace POS_MT.Controllers;

[Authorize]
public class StockMovementController : Controller
{
    private readonly IStockMovementService _stockMovementService;
    private readonly INavContextService _navContext;

    public StockMovementController(IStockMovementService stockMovementService, INavContextService navContext)
    {
        _stockMovementService = stockMovementService;
        _navContext = navContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? classification, string? navArea, CancellationToken cancellationToken)
    {
        ApplyNavArea(navArea);
        ViewData["Title"] = "Slow / Dead Stock";
        var model = await _stockMovementService.AnalyzeAsync(classification, cancellationToken);
        ViewBag.NavArea = ResolveNavArea(navArea);
        return View(model);
    }

    private void ApplyNavArea(string? navArea)
    {
        var area = ResolveNavArea(navArea);
        _navContext.SetArea(area);
    }

    private static string ResolveNavArea(string? navArea)
    {
        if (string.Equals(navArea, "Dashboard", StringComparison.OrdinalIgnoreCase))
        {
            return "Dashboard";
        }

        if (string.Equals(navArea, "Reports", StringComparison.OrdinalIgnoreCase))
        {
            return "Reports";
        }

        return "Inventories";
    }
}
