using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SMRT_QC_Web.Data;
using SMRT_QC_Web.Models;

namespace SMRT_QC_Web.Controllers;

/// <summary>
/// CRUD management for master Parts data.
/// ADMIN and MANAGER can create/edit/delete; SUPERVISOR and STAFF have read-only access.
/// </summary>
[Authorize]
public class PartController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<PartController> _logger;

    public PartController(AppDbContext db, ILogger<PartController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── GET /Parts/Index ─────────────────────────────────────────────────────

    /// <summary>Displays paginated/searchable list of all parts.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? category, int page = 1)
    {
        const int pageSize = 20;
        ViewBag.Search = search;
        ViewBag.Category = category;

        try
        {
            var query = _db.Parts.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.PartNumber.Contains(search) || p.PartName.Contains(search));

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category == category);

            // Fetch distinct categories for filter dropdown
            ViewBag.Categories = await _db.Parts
                .AsNoTracking()
                .Where(p => p.Category != null)
                .Select(p => p.Category!)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            var total = await query.CountAsync();
            var parts = await query
                .OrderBy(p => p.PartNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.TotalRecords = total;

            return View(parts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching parts list.");
            TempData["Error"] = "Gagal memuat data part.";
            return View(new List<Part>());
        }
    }

    // ─── GET /Parts/GetAll (AJAX) ─────────────────────────────────────────────

    /// <summary>
    /// Returns all parts as JSON for use in Inspection Create dropdown/autocomplete.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var parts = await _db.Parts
                .AsNoTracking()
                .OrderBy(p => p.PartNumber)
                .Select(p => new { p.Id, p.PartNumber, p.PartName, p.Category })
                .ToListAsync();

            return Json(new { success = true, data = parts });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all parts.");
            return Json(new { success = false, message = "Gagal memuat data part." });
        }
    }

    // ─── GET /Parts/GetPart (AJAX) ────────────────────────────────────────────

    /// <summary>Returns a single part's data as JSON for the edit modal.</summary>
    [HttpGet]
    public async Task<IActionResult> GetPart(int id)
    {
        try
        {
            var part = await _db.Parts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (part == null)
                return Json(new { success = false, message = "Part tidak ditemukan." });

            return Json(new { success = true, data = new { part.Id, part.PartNumber, part.PartName, part.Category, part.Spec } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching part {Id}.", id);
            return Json(new { success = false, message = "Gagal memuat data part." });
        }
    }

    // ─── POST /Parts/Create ───────────────────────────────────────────────────

    /// <summary>Creates a new part record after validating uniqueness of PartNumber.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> Create(string partNumber, string partName, string? category, string? spec)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(partNumber) || string.IsNullOrWhiteSpace(partName))
                return Json(new { success = false, message = "Part Number dan Part Name wajib diisi." });

            var exists = await _db.Parts.AnyAsync(p => p.PartNumber == partNumber);
            if (exists)
                return Json(new { success = false, message = "Part Number sudah terdaftar." });

            var part = new Part
            {
                PartNumber = partNumber.Trim().ToUpper(),
                PartName = partName.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
                Spec = string.IsNullOrWhiteSpace(spec) ? null : spec.Trim(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _db.Parts.Add(part);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Part created: {PartNumber} - {PartName}", part.PartNumber, part.PartName);
            return Json(new { success = true, message = "Part berhasil ditambahkan." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating part {PartNumber}.", partNumber);
            return Json(new { success = false, message = "Gagal menambahkan part: " + ex.Message });
        }
    }

    // ─── POST /Parts/Edit ─────────────────────────────────────────────────────

    /// <summary>Updates an existing part's details.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> Edit(int id, string partNumber, string partName, string? category, string? spec)
    {
        try
        {
            var part = await _db.Parts.FindAsync(id);
            if (part == null)
                return Json(new { success = false, message = "Part tidak ditemukan." });

            // Check uniqueness excluding current record
            var conflict = await _db.Parts.AnyAsync(p => p.PartNumber == partNumber && p.Id != id);
            if (conflict)
                return Json(new { success = false, message = "Part Number sudah digunakan oleh part lain." });

            part.PartNumber = partNumber.Trim().ToUpper();
            part.PartName = partName.Trim();
            part.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
            part.Spec = string.IsNullOrWhiteSpace(spec) ? null : spec.Trim();
            part.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            _logger.LogInformation("Part {Id} updated: {PartNumber}", id, part.PartNumber);
            return Json(new { success = true, message = "Part berhasil diperbarui." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating part {Id}.", id);
            return Json(new { success = false, message = "Gagal memperbarui part: " + ex.Message });
        }
    }

    // ─── POST /Parts/Delete ───────────────────────────────────────────────────

    /// <summary>Deletes a part. Only ADMIN may delete.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var part = await _db.Parts.FindAsync(id);
            if (part == null)
                return Json(new { success = false, message = "Part tidak ditemukan." });

            _db.Parts.Remove(part);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Part {Id} ({PartNumber}) deleted.", id, part.PartNumber);
            return Json(new { success = true, message = "Part berhasil dihapus." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting part {Id}.", id);
            return Json(new { success = false, message = "Gagal menghapus part: " + ex.Message });
        }
    }
}
