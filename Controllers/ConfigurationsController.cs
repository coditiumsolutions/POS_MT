using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize]
public class ConfigurationsController : Controller
{
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;

    public ConfigurationsController(POSDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Configs";
        var rows = await _db.Configurations.AsNoTracking()
            .OrderBy(x => x.ConfigKey)
            .ThenBy(x => x.Uid)
            .ToListAsync(ct);
        return View(rows);
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Add Config";
        return View(new ConfigurationFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ConfigurationFormViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Add Config";
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var key = model.ConfigKey.Trim();
        if (await _db.Configurations.AnyAsync(x => x.ConfigKey == key, ct))
        {
            ModelState.AddModelError(nameof(model.ConfigKey), "Config key already exists.");
            return View(model);
        }

        var entity = new Configuration
        {
            ConfigId = model.ConfigId,
            ConfigKey = key,
            ConfigValue = string.IsNullOrWhiteSpace(model.ConfigValue) ? null : model.ConfigValue.Trim()
        };

        _db.Configurations.Add(entity);
        await _db.SaveChangesAsync(ct);

        if (entity.ConfigId is null)
        {
            entity.ConfigId = entity.Uid;
            await _db.SaveChangesAsync(ct);
        }

        await _audit.WriteAsync("Create", "Configuration", entity.Uid.ToString(),
            $"Created config {entity.ConfigKey}", ct);
        TempData["Success"] = "Configuration created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        ViewData["Title"] = "Edit Config";
        var entity = await _db.Configurations.FindAsync([id], ct);
        if (entity is null)
        {
            return NotFound();
        }

        return View(new ConfigurationFormViewModel
        {
            Uid = entity.Uid,
            ConfigId = entity.ConfigId,
            ConfigKey = entity.ConfigKey ?? string.Empty,
            ConfigValue = entity.ConfigValue
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ConfigurationFormViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Edit Config";
        if (id != model.Uid)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entity = await _db.Configurations.FindAsync([id], ct);
        if (entity is null)
        {
            return NotFound();
        }

        var key = model.ConfigKey.Trim();
        if (await _db.Configurations.AnyAsync(x => x.ConfigKey == key && x.Uid != id, ct))
        {
            ModelState.AddModelError(nameof(model.ConfigKey), "Config key already exists.");
            return View(model);
        }

        entity.ConfigId = model.ConfigId;
        entity.ConfigKey = key;
        entity.ConfigValue = string.IsNullOrWhiteSpace(model.ConfigValue) ? null : model.ConfigValue.Trim();
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "Configuration", entity.Uid.ToString(),
            $"Updated config {entity.ConfigKey}", ct);
        TempData["Success"] = "Configuration updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        ViewData["Title"] = "Config Details";
        var entity = await _db.Configurations.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        return entity is null ? NotFound() : View(entity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.Configurations.FindAsync([id], ct);
        if (entity is null)
        {
            TempData["Error"] = "Configuration was not found.";
            return RedirectToAction(nameof(Index));
        }

        var key = entity.ConfigKey ?? id.ToString();
        _db.Configurations.Remove(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Delete", "Configuration", id.ToString(),
            $"Deleted config {key}", ct);
        TempData["Success"] = "Configuration deleted.";
        return RedirectToAction(nameof(Index));
    }
}
