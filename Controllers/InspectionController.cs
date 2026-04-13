using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SMRT_QC_Web.Data;
using SMRT_QC_Web.Hubs;
using SMRT_QC_Web.Models;

namespace SMRT_QC_Web.Controllers;

/// <summary>
/// Manages inspection records lifecycle: list, create, detail view,
/// status transitions (CHECK → VALIDATION → MAKE NCN → DONE).
/// Sends SignalR notifications on status changes.
/// </summary>
[Authorize]
public class InspectionController : Controller
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<InspectionController> _logger;

    public InspectionController(
        AppDbContext db,
        IHubContext<NotificationHub> hub,
        ILogger<InspectionController> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    // ─── GET /Inspection/Index ────────────────────────────────────────────────

    /// <summary>
    /// Displays paginated list of all inspection records with filters
    /// for status, part number, supply number and date range.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string? partNumber,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page = 1)
    {
        const int pageSize = 25;
        ViewBag.Search = search;
        ViewBag.StatusFilter = status;
        ViewBag.PartFilter = partNumber;
        ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
        ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");

        try
        {
            var query = _db.InspectionRecords.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(r =>
                    (r.SupplyNumber != null && r.SupplyNumber.Contains(search)) ||
                    (r.PoNumber != null && r.PoNumber.Contains(search)) ||
                    (r.Operator != null && r.Operator.Contains(search)));

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.NcnStatus == status);

            if (!string.IsNullOrWhiteSpace(partNumber))
                query = query.Where(r => r.PartNumber == partNumber);

            if (dateFrom.HasValue)
                query = query.Where(r => r.SupplyDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(r => r.SupplyDate <= dateTo.Value.AddDays(1));

            var total = await query.CountAsync();
            var records = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.TotalRecords = total;

            // Parts list for filter dropdown
            ViewBag.Parts = await _db.Parts
                .AsNoTracking()
                .OrderBy(p => p.PartNumber)
                .Select(p => new { p.PartNumber, p.PartName })
                .ToListAsync();

            return View(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching inspection list.");
            TempData["Error"] = "Gagal memuat data inspeksi.";
            return View(new List<InspectionRecord>());
        }
    }

    // ─── GET /Inspection/Create ───────────────────────────────────────────────

    /// <summary>Displays the new inspection input form with parts dropdown pre-loaded.</summary>
    [HttpGet]
    [Authorize(Roles = "ADMIN,MANAGER,SUPERVISOR,STAFF")]
    public async Task<IActionResult> Create()
    {
        try
        {
            ViewBag.Parts = await _db.Parts
                .AsNoTracking()
                .OrderBy(p => p.PartNumber)
                .ToListAsync();

            // Generate a default supply number using today's date + sequence
            var todayCount = await _db.InspectionRecords
                .CountAsync(r => r.CreatedAt.Date == DateTime.Today);
            ViewBag.DefaultSupplyNo = $"SPL-{DateTime.Now:yyyyMMdd}-{(todayCount + 1):D3}";

            return View(new InspectionRecord());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Create inspection form.");
            TempData["Error"] = "Gagal memuat form inspeksi.";
            return RedirectToAction(nameof(Index));
        }
    }

    // ─── POST /Inspection/Create ──────────────────────────────────────────────

    /// <summary>
    /// Saves a new inspection record. Sends a SignalR notification to
    /// the SUPERVISOR group to inform them of a new inspection needing review.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ADMIN,MANAGER,SUPERVISOR,STAFF")]
    public async Task<IActionResult> Create(InspectionRecord model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Parts = await _db.Parts.AsNoTracking().OrderBy(p => p.PartNumber).ToListAsync();
                return View(model);
            }

            model.NcnStatus = "CHECK";
            model.ChekName = User.Identity?.Name;
            model.CreatedAt = DateTime.Now;
            model.UpdatedAt = DateTime.Now;

            _db.InspectionRecords.Add(model);
            await _db.SaveChangesAsync();

            // Save persistent notification record
            var notif = new Notification
            {
                SuppNumber = model.SupplyNumber,
                PartNumber = model.PartNumber,
                Position = "SUPERVISOR",
                Notif = $"Inspeksi baru: Supply {model.SupplyNumber} - Part {model.PartNumber} perlu direview.",
                IsRead = false,
                CreatedAt = DateTime.Now
            };
            _db.Notifications.Add(notif);
            await _db.SaveChangesAsync();

            // Broadcast real-time via SignalR
            await NotificationHub.SendToRole(
                _hub, "SUPERVISOR",
                $"Inspeksi baru masuk: {model.SupplyNumber} [{model.PartNumber}]",
                "info");

            _logger.LogInformation("Inspection created: {SupplyNumber}", model.SupplyNumber);
            TempData["Success"] = "Inspeksi berhasil disimpan.";
            return RedirectToAction(nameof(Detail), new { id = model.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating inspection.");
            ModelState.AddModelError(string.Empty, "Gagal menyimpan inspeksi: " + ex.Message);
            ViewBag.Parts = await _db.Parts.AsNoTracking().OrderBy(p => p.PartNumber).ToListAsync();
            return View(model);
        }
    }

    // ─── GET /Inspection/Detail/{id} ─────────────────────────────────────────

    /// <summary>Displays full detail of a single inspection record with status history.</summary>
    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        try
        {
            var record = await _db.InspectionRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (record == null)
            {
                TempData["Error"] = "Rekord inspeksi tidak ditemukan.";
                return RedirectToAction(nameof(Index));
            }

            // Fetch related check sheets for this part
            ViewBag.CheckSheets = await _db.CheckSheets
                .AsNoTracking()
                .Where(cs => cs.PartNumber == record.PartNumber)
                .ToListAsync();

            // Fetch related NCN if any
            if (!string.IsNullOrWhiteSpace(record.NcnNo))
            {
                ViewBag.NcnRecord = await _db.NcnRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.NcnNo == record.NcnNo);
            }

            return View(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading inspection detail {Id}.", id);
            TempData["Error"] = "Gagal memuat detail inspeksi.";
            return RedirectToAction(nameof(Index));
        }
    }

    // ─── POST /Inspection/UpdateStatus ───────────────────────────────────────

    /// <summary>
    /// Advances the inspection status to the next workflow state.
    /// Notifies relevant roles via SignalR on each transition.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string newStatus)
    {
        try
        {
            var validStatuses = new[] { "CHECK", "VALIDATION", "MAKE NCN", "DONE" };
            if (!validStatuses.Contains(newStatus))
                return Json(new { success = false, message = "Status tidak valid." });

            var record = await _db.InspectionRecords.FindAsync(id);
            if (record == null)
                return Json(new { success = false, message = "Rekord tidak ditemukan." });

            var oldStatus = record.NcnStatus;
            record.NcnStatus = newStatus;
            record.UpdatedAt = DateTime.Now;

            // Set approver name when moved to VALIDATION
            if (newStatus == "VALIDATION")
                record.AppvName = User.Identity?.Name;

            await _db.SaveChangesAsync();

            // Determine notification target based on new status
            var (targetRole, notifMsg) = newStatus switch
            {
                "VALIDATION" => ("MANAGER", $"Inspeksi {record.SupplyNumber} siap divalidasi."),
                "MAKE NCN"   => ("SUPERVISOR", $"NCN perlu dibuat untuk {record.SupplyNumber}."),
                "DONE"       => ("ALL", $"Inspeksi {record.SupplyNumber} telah selesai."),
                _            => ("SUPERVISOR", $"Status inspeksi {record.SupplyNumber} diperbarui: {newStatus}")
            };

            // Persist notification
            _db.Notifications.Add(new Notification
            {
                SuppNumber = record.SupplyNumber,
                PartNumber = record.PartNumber,
                Position = targetRole,
                Notif = notifMsg,
                IsRead = false,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();

            // Real-time broadcast
            await NotificationHub.SendToRole(_hub, targetRole, notifMsg,
                newStatus == "MAKE NCN" ? "warning" : "info");

            _logger.LogInformation("Inspection {Id} status: {Old} → {New}", id, oldStatus, newStatus);
            return Json(new { success = true, message = $"Status berhasil diubah ke {newStatus}." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inspection {Id} status.", id);
            return Json(new { success = false, message = "Gagal memperbarui status: " + ex.Message });
        }
    }

    // ─── POST /Inspection/Validate ────────────────────────────────────────────

    /// <summary>
    /// Validates (APPROVED/REJECTED) an inspection by MANAGER or above.
    /// Sets the Validasi field and advances status accordingly.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> Validate(int id, string result, string? notes)
    {
        try
        {
            if (result != "APPROVED" && result != "REJECTED")
                return Json(new { success = false, message = "Hasil validasi tidak valid." });

            var record = await _db.InspectionRecords.FindAsync(id);
            if (record == null)
                return Json(new { success = false, message = "Rekord tidak ditemukan." });

            record.Validasi = result;
            record.AppvName = User.Identity?.Name;
            record.NcnStatus = result == "APPROVED" ? "DONE" : "MAKE NCN";
            record.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            var notifMsg = result == "APPROVED"
                ? $"Inspeksi {record.SupplyNumber} diapprove oleh {record.AppvName}."
                : $"Inspeksi {record.SupplyNumber} ditolak — NCN harus dibuat.";

            _db.Notifications.Add(new Notification
            {
                SuppNumber = record.SupplyNumber,
                PartNumber = record.PartNumber,
                Position = "SUPERVISOR",
                Notif = notifMsg,
                IsRead = false,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();

            await NotificationHub.SendToRole(_hub, "SUPERVISOR", notifMsg,
                result == "APPROVED" ? "success" : "danger");

            return Json(new
            {
                success = true,
                message = $"Inspeksi berhasil {(result == "APPROVED" ? "diapprove" : "direject")}.",
                newStatus = record.NcnStatus
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating inspection {Id}.", id);
            return Json(new { success = false, message = "Gagal melakukan validasi: " + ex.Message });
        }
    }

    // ─── POST /Inspection/Delete ──────────────────────────────────────────────

    /// <summary>Deletes an inspection record. Only ADMIN may delete.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var record = await _db.InspectionRecords.FindAsync(id);
            if (record == null)
                return Json(new { success = false, message = "Rekord tidak ditemukan." });

            _db.InspectionRecords.Remove(record);
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Rekord inspeksi berhasil dihapus." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting inspection {Id}.", id);
            return Json(new { success = false, message = "Gagal menghapus rekord: " + ex.Message });
        }
    }
}
