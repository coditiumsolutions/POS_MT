using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
namespace POS_MT.Controllers;
[Authorize]
public class AuditLogsController : Controller {
  private readonly POSDbContext _db;
  public AuditLogsController(POSDbContext db){ _db=db; }
  public async Task<IActionResult> Index(CancellationToken ct)=>View(await _db.AuditLogs.AsNoTracking().OrderByDescending(x=>x.LogDate).Take(500).ToListAsync(ct));
  public async Task<IActionResult> Details(long id, CancellationToken ct){ var e=await _db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
}
