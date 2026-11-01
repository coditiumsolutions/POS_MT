using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize(Policy = "ManagerOrAdmin")]
public class InventoriesController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit; private readonly INavContextService _navContext;
  public InventoriesController(POSDbContext db, IAuditService audit, INavContextService navContext){ _db=db; _audit=audit; _navContext=navContext; }
  private void EnsureInventoriesNav() => _navContext.SetArea("AllModules");
  private async Task LoadLookups(CancellationToken ct){
    ViewBag.Products = new SelectList(await _db.Products.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.ProductName).ToListAsync(ct), "Uid", "ProductName");
    ViewBag.Warehouses = new SelectList(await _db.Warehouses.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.WarehouseName).ToListAsync(ct), "Uid", "WarehouseName");
  }
  public async Task<IActionResult> Index(CancellationToken ct){
    EnsureInventoriesNav();
    return View(await LoadInventoryBalanceRowsAsync(orderByUidDesc: true, ct));
  }

  /// <summary>Printable Inventory Balance: Product Name, Branch, Current Quantity.</summary>
  [HttpGet]
  public async Task<IActionResult> PrintBalance(CancellationToken ct)
  {
    EnsureInventoriesNav();
    var rows = await LoadInventoryBalanceRowsAsync(orderByUidDesc: false, ct);
    var model = new InventoryBalancePrintViewModel
    {
      PrintedAt = DateTime.Now,
      Rows = rows
        .OrderBy(x => x.ProductName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(x => x.BranchName, StringComparer.OrdinalIgnoreCase)
        .ToList()
    };
    return View(model);
  }

  private async Task<List<InventoryListItemViewModel>> LoadInventoryBalanceRowsAsync(bool orderByUidDesc, CancellationToken ct)
  {
    var query =
      from i in _db.Inventories.AsNoTracking()
      join p in _db.Products.AsNoTracking() on i.ProductUid equals p.Uid into pj
      from p in pj.DefaultIfEmpty()
      join w in _db.Warehouses.AsNoTracking() on i.WarehouseUid equals w.Uid into wj
      from w in wj.DefaultIfEmpty()
      join b in _db.Branches.AsNoTracking() on w.BranchUid equals b.Uid into bj
      from b in bj.DefaultIfEmpty()
      select new InventoryListItemViewModel
      {
        Uid = i.Uid,
        ProductName = p != null ? p.ProductName : i.ProductUid.ToString(),
        BranchName = b != null ? b.BranchName : (w != null ? w.WarehouseName : "—"),
        CurrentQuantity = i.CurrentQuantity,
        AverageCost = i.AverageCost,
        LastPurchasePrice = i.LastPurchasePrice,
        LastSalePrice = i.LastSalePrice
      };

    return orderByUidDesc
      ? await query.OrderByDescending(x => x.Uid).ToListAsync(ct)
      : await query.ToListAsync(ct);
  }
  public async Task<IActionResult> Create(CancellationToken ct){ EnsureInventoriesNav(); await LoadLookups(ct); return View(new InventoryFormViewModel()); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(InventoryFormViewModel model, CancellationToken ct){
    EnsureInventoriesNav();
    if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    if(await _db.Inventories.AnyAsync(x=>x.ProductUid==model.ProductUid && x.WarehouseUid==model.WarehouseUid,ct)){ ModelState.AddModelError(string.Empty,"Inventory already exists for this product and warehouse."); await LoadLookups(ct); return View(model);} 
    var e=new Inventory{ProductUid=model.ProductUid,WarehouseUid=model.WarehouseUid,QuantityIn=0,QuantityOut=0,CurrentQuantity=0,AverageCost=0,LastPurchasePrice=0,LastSalePrice=0,MinimumStock=model.MinimumStock,MaximumStock=model.MaximumStock,LastStockDate=DateTime.Now,UpdatedDate=DateTime.Now};
    _db.Inventories.Add(e); await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Create","Inventory",e.Uid.ToString(),"Created inventory row",ct);
    TempData["Success"]="Inventory created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ EnsureInventoriesNav(); var e=await _db.Inventories.FindAsync([id],ct); if(e is null) return NotFound(); await LoadLookups(ct); return View(new InventoryFormViewModel{Uid=e.Uid,ProductUid=e.ProductUid,WarehouseUid=e.WarehouseUid,QuantityIn=e.QuantityIn,QuantityOut=e.QuantityOut,CurrentQuantity=e.CurrentQuantity,AverageCost=e.AverageCost,LastPurchasePrice=e.LastPurchasePrice,LastSalePrice=e.LastSalePrice,MinimumStock=e.MinimumStock,MaximumStock=e.MaximumStock}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, InventoryFormViewModel model, CancellationToken ct){
    EnsureInventoriesNav();
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    var e=await _db.Inventories.FindAsync([id],ct); if(e is null) return NotFound();
    e.MinimumStock=model.MinimumStock; e.MaximumStock=model.MaximumStock; e.UpdatedDate=DateTime.Now;
    await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","Inventory",e.Uid.ToString(),"Updated inventory row",ct);
    TempData["Success"]="Inventory updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ EnsureInventoriesNav(); var e=await _db.Inventories.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Delete(int id, CancellationToken ct)
  {
    EnsureInventoriesNav();
    var entity = await _db.Inventories.FindAsync([id], ct);
    if (entity is null)
    {
      return NotFound();
    }

    var label = $"product {entity.ProductUid} / warehouse {entity.WarehouseUid}";
    _db.Inventories.Remove(entity);
    await _db.SaveChangesAsync(ct);
    await _audit.WriteAsync("Delete", "Inventory", id.ToString(), $"Deleted inventory row ({label})", ct);
    TempData["Success"] = "Inventory deleted.";
    return RedirectToAction(nameof(Index));
  }
}
