using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class InventoriesController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit;
  public InventoriesController(POSDbContext db, IAuditService audit){ _db=db; _audit=audit; }
  private async Task LoadLookups(CancellationToken ct){
    ViewBag.Products = new SelectList(await _db.Products.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.ProductName).ToListAsync(ct), "Uid", "ProductName");
    ViewBag.Warehouses = new SelectList(await _db.Warehouses.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.WarehouseName).ToListAsync(ct), "Uid", "WarehouseName");
  }
  public async Task<IActionResult> Index(CancellationToken ct){
    var rows = await (from i in _db.Inventories.AsNoTracking()
      join p in _db.Products.AsNoTracking() on i.ProductUid equals p.Uid into pj from p in pj.DefaultIfEmpty()
      join w in _db.Warehouses.AsNoTracking() on i.WarehouseUid equals w.Uid into wj from w in wj.DefaultIfEmpty()
      orderby i.Uid descending
      select new InventoryListItemViewModel { Uid=i.Uid, ProductName=p!=null?p.ProductName:i.ProductUid.ToString(), WarehouseName=w!=null?w.WarehouseName:i.WarehouseUid.ToString(), CurrentQuantity=i.CurrentQuantity, AverageCost=i.AverageCost, LastPurchasePrice=i.LastPurchasePrice, LastSalePrice=i.LastSalePrice }).ToListAsync(ct);
    return View(rows);
  }
  public async Task<IActionResult> Create(CancellationToken ct){ await LoadLookups(ct); return View(new InventoryFormViewModel()); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(InventoryFormViewModel model, CancellationToken ct){
    if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    if(await _db.Inventories.AnyAsync(x=>x.ProductUid==model.ProductUid && x.WarehouseUid==model.WarehouseUid,ct)){ ModelState.AddModelError(string.Empty,"Inventory already exists for this product and warehouse."); await LoadLookups(ct); return View(model);} 
    var e=new Inventory{ProductUid=model.ProductUid,WarehouseUid=model.WarehouseUid,QuantityIn=model.QuantityIn,QuantityOut=model.QuantityOut,CurrentQuantity=model.CurrentQuantity,AverageCost=model.AverageCost,LastPurchasePrice=model.LastPurchasePrice,LastSalePrice=model.LastSalePrice,MinimumStock=model.MinimumStock,MaximumStock=model.MaximumStock,LastStockDate=DateTime.Now,UpdatedDate=DateTime.Now};
    _db.Inventories.Add(e); await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Create","Inventory",e.Uid.ToString(),"Created inventory row",ct);
    TempData["Success"]="Inventory created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ var e=await _db.Inventories.FindAsync([id],ct); if(e is null) return NotFound(); await LoadLookups(ct); return View(new InventoryFormViewModel{Uid=e.Uid,ProductUid=e.ProductUid,WarehouseUid=e.WarehouseUid,QuantityIn=e.QuantityIn,QuantityOut=e.QuantityOut,CurrentQuantity=e.CurrentQuantity,AverageCost=e.AverageCost,LastPurchasePrice=e.LastPurchasePrice,LastSalePrice=e.LastSalePrice,MinimumStock=e.MinimumStock,MaximumStock=e.MaximumStock}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, InventoryFormViewModel model, CancellationToken ct){
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    var e=await _db.Inventories.FindAsync([id],ct); if(e is null) return NotFound();
    e.ProductUid=model.ProductUid; e.WarehouseUid=model.WarehouseUid; e.QuantityIn=model.QuantityIn; e.QuantityOut=model.QuantityOut; e.CurrentQuantity=model.CurrentQuantity; e.AverageCost=model.AverageCost; e.LastPurchasePrice=model.LastPurchasePrice; e.LastSalePrice=model.LastSalePrice; e.MinimumStock=model.MinimumStock; e.MaximumStock=model.MaximumStock; e.UpdatedDate=DateTime.Now;
    await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","Inventory",e.Uid.ToString(),"Updated inventory row",ct);
    TempData["Success"]="Inventory updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ var e=await _db.Inventories.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
}
