using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SMRT_QC_Web.Data;
using SMRT_QC_Web.Hubs;
using SMRT_QC_Web.Models;

namespace SMRT_QC_Web.Controllers;

/// <summary>
/// Manages Non-Conformance Notice (NCN) records.
/// Supports CRUD operations and photo upload (browser camera or file input).
/// Photos are stored in wwwroot/uploads/ with unique filenames.
/// </summary>
[Authorize]
public class NcnController : Controller
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<NcnController> _logger;

    public NcnController(
        AppDbContext db,
        IHubContext<NotificationHub> hub,
        IWebHostEnvironment env,
        ILogger<NcnController> logger)
    {
        _db = db;
        _hub = hub;
        _env = env;
        _logger = logger;
    }

    // ─── GET /Ncn/Index ───────────────────────────────────────────────────────

    /// <summary>Displays paginated NCN list with filters for status and part number.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? status, string? partNumber, int page = 1)
    {
        const int pageSize = 25;
        ViewBag.Search = search;
        ViewBag.StatusFilter = status;
        ViewBag.PartFilter = partNumber;

        try
        {
            var query = _db.NcnRecords.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(n =>
                    n.NcnNo.Contains(search) ||
                    (n.SupplyNumber != null && n.SupplyNumber.Contains(search)) ||
                    (n.PartNumber != null && n.PartNumber.Contains(search)));

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(n => n.Status == status);

            if (!string.IsNullOrWhiteSpace(partNumber))
                query = query.Where(n => n.PartNumber == partNumber);

            var total = await query.CountAsync();
            var records = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.TotalRecords = total;

            ViewBag.Parts = await _db.Parts
                .AsNoTracking()
                .OrderBy(p => p.PartNumber)
                .Select(p => new { p.PartNumber, p.PartName })
                .ToListAsync();

            return View(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching NCN list.");
            TempData["Error"] = "Gagal memuat data NCN.";
            return View(new List<NcnRecord>());
        }
    }

    // ─── GET /Ncn/Create ──────────────────────────────────────────────────────

    /// <summary>
    /// Displays the NCN creation form. If a supplyNumber query param is provided,
    /// pre-populates the form from the linked inspection record.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(string? supplyNumber = null)
    {
        try
        {
            ViewBag.Parts = await _db.Parts
                .AsNoTracking()
                .OrderBy(p => p.PartNumber)
                .ToListAsync();

            // Auto-generate NCN number
            var todayCount = await _db.NcnRecords.CountAsync(n => n.CreatedAt.Date == DateTime.Today);
            var autoNcnNo = $"NCN-{DateTime.Now:yyyyMMdd}-{(todayCount + 1):D3}";
            ViewBag.DefaultNcnNo = autoNcnNo;

            var model = new NcnRecord
            {
                NcnNo = autoNcnNo,
                CheckerName = User.Identity?.Name,
                Status = "CHECK"
            };

            // Pre-fill from inspection if supply number is linked
            if (!string.IsNullOrWhiteSpace(supplyNumber))
            {
                var inspection = await _db.InspectionRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.SupplyNumber == supplyNumber);

                if (inspection != null)
                {
                    model.SupplyNumber = inspection.SupplyNumber;
                    model.PartNumber = inspection.PartNumber;
                }
            }

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading NCN create form.");
            TempData["Error"] = "Gagal memuat form NCN.";
            return RedirectToAction(nameof(Index));
        }
    }

    // ─── POST /Ncn/Create ─────────────────────────────────────────────────────

    /// <summary>
    /// Saves a new NCN record. Accepts an optional IFormFile photo upload
    /// (from file input or base64 camera capture via hidden input).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NcnRecord model, IFormFile? photoFile, string? photoBase64)
    {
        try
        {
            // Remove photo path from validation since it's set programmatically
            ModelState.Remove(nameof(NcnRecord.PhotoPath));
            ModelState.Remove(nameof(NcnRecord.Status));

            if (!ModelState.IsValid)
            {
                ViewBag.Parts = await _db.Parts.AsNoTracking().OrderBy(p => p.PartNumber).ToListAsync();
                return View(model);
            }

            // Check NCN number uniqueness
            var exists = await _db.NcnRecords.AnyAsync(n => n.NcnNo == model.NcnNo);
            if (exists)
            {
                ModelState.AddModelError(nameof(NcnRecord.NcnNo), "NCN Number sudah digunakan.");
                ViewBag.Parts = await _db.Parts.AsNoTracking().OrderBy(p => p.PartNumber).ToListAsync();
                return View(model);
            }

            // Handle photo upload (file input takes priority over base64 camera capture)
            model.PhotoPath = await SavePhoto(photoFile, photoBase64);
            model.CheckerName = User.Identity?.Name;
            model.Status = "CHECK";
            model.CreatedAt = DateTime.Now;
            model.UpdatedAt = DateTime.Now;

            _db.NcnRecords.Add(model);
            await _db.SaveChangesAsync();

            // Update linked inspection record if supply number matches
            var inspection = await _db.InspectionRecords
                .FirstOrDefaultAsync(r => r.SupplyNumber == model.SupplyNumber);
            if (inspection != null)
            {
                inspection.NcnNo = model.NcnNo;
                inspection.NcnStatus = "MAKE NCN";
                inspection.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }

            // Notify MANAGER of new NCN
            var notifMsg = $"NCN baru dibuat: {model.NcnNo} untuk Part {model.PartNumber}";
            _db.Notifications.Add(new Notification
            {
                SuppNumber = model.SupplyNumber,
                PartNumber = model.PartNumber,
                Position = "MANAGER",
                Notif = notifMsg,
                IsRead = false,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();

            await NotificationHub.SendToRole(_hub, "MANAGER", notifMsg, "warning");

            _logger.LogInformation("NCN created: {NcnNo}", model.NcnNo);
            TempData["Success"] = "NCN berhasil dibuat.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating NCN.");
            ModelState.AddModelError(string.Empty, "Gagal menyimpan NCN: " + ex.Message);
            ViewBag.Parts = await _db.Parts.AsNoTracking().OrderBy(p => p.PartNumber).ToListAsync();
            return View(model);
        }
    }

    // ─── GET /Ncn/GetNcn (AJAX) ───────────────────────────────────────────────

    /// <summary>Returns full NCN detail as JSON for the detail modal popup.</summary>
    [HttpGet]
    public async Task<IActionResult> GetNcn(int id)
    {
        try
        {
            var ncn = await _db.NcnRecords.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
            if (ncn == null)
                return Json(new { success = false, message = "NCN tidak ditemukan." });

            return Json(new
            {
                success = true,
                data = new
                {
                    ncn.Id,
                    ncn.NcnNo,
                    ncn.SupplyNumber,
                    ncn.PartNumber,
                    ncn.DefectType,
                    ncn.DefectQty,
                    ncn.CheckerName,
                    ncn.ApproverName,
                    ncn.Status,
                    ncn.PhotoPath,
                    ncn.Notes,
                    CreatedAt = ncn.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    HasPhoto = ncn.HasPhoto,
                    StatusBadge = ncn.StatusBadgeClass
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching NCN {Id}.", id);
            return Json(new { success = false, message = "Gagal memuat data NCN." });
        }
    }

    // ─── POST /Ncn/UpdateStatus ───────────────────────────────────────────────

    /// <summary>
    /// Advances the NCN status: CHECK → PROCESS → DONE.
    /// Sets the approver name when moving to PROCESS or DONE.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ADMIN,MANAGER,SUPERVISOR")]
    public async Task<IActionResult> UpdateStatus(int id, string newStatus)
    {
        try
        {
            var validStatuses = new[] { "CHECK", "PROCESS", "DONE" };
            if (!validStatuses.Contains(newStatus))
                return Json(new { success = false, message = "Status tidak valid." });

            var ncn = await _db.NcnRecords.FindAsync(id);
            if (ncn == null)
                return Json(new { success = false, message = "NCN tidak ditemukan." });

            ncn.Status = newStatus;
            ncn.UpdatedAt = DateTime.Now;

            if (newStatus is "PROCESS" or "DONE")
                ncn.ApproverName = User.Identity?.Name;

            await _db.SaveChangesAsync();

            // Notify SUPERVISOR when NCN is closed
            if (newStatus == "DONE")
            {
                var msg = $"NCN {ncn.NcnNo} telah diselesaikan oleh {ncn.ApproverName}.";
                _db.Notifications.Add(new Notification
                {
                    SuppNumber = ncn.SupplyNumber,
                    PartNumber = ncn.PartNumber,
                    Position = "SUPERVISOR",
                    Notif = msg,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
                await _db.SaveChangesAsync();
                await NotificationHub.SendToRole(_hub, "SUPERVISOR", msg, "success");
            }

            return Json(new { success = true, message = $"Status NCN berhasil diubah ke {newStatus}." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating NCN {Id} status.", id);
            return Json(new { success = false, message = "Gagal memperbarui status NCN: " + ex.Message });
        }
    }

    // ─── POST /Ncn/Delete ─────────────────────────────────────────────────────

    /// <summary>Deletes a NCN record and its associated photo file if present.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var ncn = await _db.NcnRecords.FindAsync(id);
            if (ncn == null)
                return Json(new { success = false, message = "NCN tidak ditemukan." });

            // Delete photo file from disk if it exists
            if (!string.IsNullOrWhiteSpace(ncn.PhotoPath))
            {
                var photoFull = Path.Combine(_env.WebRootPath, ncn.PhotoPath.TrimStart('/'));
                if (System.IO.File.Exists(photoFull))
                    System.IO.File.Delete(photoFull);
            }

            _db.NcnRecords.Remove(ncn);
            await _db.SaveChangesAsync();

            _logger.LogInformation("NCN {NcnNo} deleted.", ncn.NcnNo);
            return Json(new { success = true, message = "NCN berhasil dihapus." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting NCN {Id}.", id);
            return Json(new { success = false, message = "Gagal menghapus NCN: " + ex.Message });
        }
    }

    // ─── Private helper: save uploaded/captured photo ─────────────────────────

    /// <summary>
    /// Saves photo to wwwroot/uploads/. Handles both IFormFile (standard upload)
    /// and base64 string (from browser camera capture via getUserMedia).
    /// Returns the relative URL path or null if no photo provided.
    /// </summary>
    private async Task<string?> SavePhoto(IFormFile? photoFile, string? photoBase64)
    {
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir); // ensure directory exists

        // Priority 1: file input upload
        if (photoFile != null && photoFile.Length > 0)
        {
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
            if (!allowedTypes.Contains(photoFile.ContentType.ToLower()))
                return null;

            var extension = Path.GetExtension(photoFile.FileName).ToLower();
            var fileName = $"ncn_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N[..8]}{extension}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await photoFile.CopyToAsync(stream);

            return $"/uploads/{fileName}";
        }

        // Priority 2: base64 camera capture (data:image/jpeg;base64,...)
        if (!string.IsNullOrWhiteSpace(photoBase64) && photoBase64.StartsWith("data:image"))
        {
            try
            {
                var base64Data = photoBase64.Substring(photoBase64.IndexOf(',') + 1);
                var bytes = Convert.FromBase64String(base64Data);
                var fileName = $"ncn_cam_{DateTime.Now:yyyyMMddHHmmssfff}.jpg";
                var filePath = Path.Combine(uploadsDir, fileName);
                await System.IO.File.WriteAllBytesAsync(filePath, bytes);
                return $"/uploads/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save base64 camera photo.");
                return null;
            }
        }

        return null;
    }
}
