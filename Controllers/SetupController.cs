using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.Services;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize]
public class SetupController : Controller
{
    public const string PosItemLayoutKey = "PosItemLayout";

    private readonly POSDbContext _db;
    private readonly IAuditService _audit;

    public SetupController(POSDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var layout = await _db.Configurations.AsNoTracking()
            .Where(x => x.ConfigKey == PosItemLayoutKey)
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync(ct);

        var model = new ModuleCardsViewModel
        {
            PageTitle = "Setup",
            PageSubtitle = "Configure system settings and master data.",
            Modules = NavigationCatalog.SetupModules.ToList(),
            PosItemLayout = IsCardLayout(layout) ? "Card" : "Grid"
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePosLayout(string? layout, CancellationToken ct)
    {
        var value = IsCardLayout(layout) ? "Card" : "Grid";
        var row = await _db.Configurations
            .Where(x => x.ConfigKey == PosItemLayoutKey)
            .OrderBy(x => x.Uid)
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            row = new Configuration
            {
                ConfigKey = PosItemLayoutKey,
                ConfigValue = value
            };
            _db.Configurations.Add(row);
            await _db.SaveChangesAsync(ct);
            if (row.ConfigId is null)
            {
                row.ConfigId = row.Uid;
                await _db.SaveChangesAsync(ct);
            }
        }
        else
        {
            row.ConfigValue = value;
            await _db.SaveChangesAsync(ct);
        }

        await _audit.WriteAsync("Update", "Configuration", row.Uid.ToString(),
            $"POS item layout set to {value}", ct);
        TempData["Success"] = value == "Card"
            ? "Cash page will show category buttons and item cards."
            : "Cash page will show the product grid.";
        return RedirectToAction(nameof(Index));
    }

    internal static bool IsCardLayout(string? value)
        => string.Equals(value, "Card", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, "Cards", StringComparison.OrdinalIgnoreCase);
}
