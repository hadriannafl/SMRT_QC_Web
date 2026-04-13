using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SMRT_QC_Web.Data;

namespace SMRT_QC_Web.Controllers;

/// <summary>
/// Generates HTML-based reports rendered in a dedicated print layout.
/// The browser's native print dialog (window.print()) is used instead of Crystal Reports.
/// Reports include: Inspection Summary, Quality Data, and NCN Report.
/// </summary>
[Authorize]
public class ReportController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReportController> _logger;

    public ReportController(AppDbContext db, ILogger<ReportController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── GET /Reports/Index ───────────────────────────────────────────────────

    /// <summary>Displays the report filter form where user selects type and date range.</summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            ViewBag.Parts = await _db.Parts
                .AsNoTracking()
                .OrderBy(p => p.PartNumber)
                .Select(p => new { p.PartNumber, p.PartName })
                .ToListAsync();

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading report index.");
            return View();
        }
    }

    // ─── GET /Reports/InspectionReport ───────────────────────────────────────

    /// <summary>
    /// Renders a printable HTML inspection summary report.
    /// Opens in a new print-only layout (no sidebar/navbar).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> InspectionReport(
        DateTime? dateFrom,
        DateTime? dateTo,
        string? partNumber,
        string? status)
    {
        try
        {
            var query = _db.InspectionRecords.AsNoTracking();

            if (dateFrom.HasValue)
                query = query.Where(r => r.SupplyDate >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(r => r.SupplyDate <= dateTo.Value.AddDays(1));

            if (!string.IsNullOrWhiteSpace(partNumber))
                query = query.Where(r => r.PartNumber == partNumber);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.NcnStatus == status);

            var records = await query.OrderByDescending(r => r.SupplyDate).ToListAsync();

            ViewBag.DateFrom = dateFrom?.ToString("dd/MM/yyyy") ?? "-";
            ViewBag.DateTo = dateTo?.ToString("dd/MM/yyyy") ?? "-";
            ViewBag.PartNumber = partNumber ?? "Semua";
            ViewBag.Status = status ?? "Semua";
            ViewBag.PrintedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            ViewBag.PrintedBy = User.Identity?.Name;

            return View(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating inspection report.");
            TempData["Error"] = "Gagal membuat laporan inspeksi.";
            return RedirectToAction(nameof(Index));
        }
    }

    // ─── GET /Reports/NcnReport ───────────────────────────────────────────────

    /// <summary>
    /// Renders a printable HTML NCN report filtered by date range, part, and status.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> NcnReport(
        DateTime? dateFrom,
        DateTime? dateTo,
        string? partNumber,
        string? status)
    {
        try
        {
            var query = _db.NcnRecords.AsNoTracking();

            if (dateFrom.HasValue)
                query = query.Where(n => n.CreatedAt >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(n => n.CreatedAt <= dateTo.Value.AddDays(1));

            if (!string.IsNullOrWhiteSpace(partNumber))
                query = query.Where(n => n.PartNumber == partNumber);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(n => n.Status == status);

            var records = await query.OrderByDescending(n => n.CreatedAt).ToListAsync();

            ViewBag.DateFrom = dateFrom?.ToString("dd/MM/yyyy") ?? "-";
            ViewBag.DateTo = dateTo?.ToString("dd/MM/yyyy") ?? "-";
            ViewBag.PartNumber = partNumber ?? "Semua";
            ViewBag.Status = status ?? "Semua";
            ViewBag.PrintedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            ViewBag.PrintedBy = User.Identity?.Name;

            return View(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating NCN report.");
            TempData["Error"] = "Gagal membuat laporan NCN.";
            return RedirectToAction(nameof(Index));
        }
    }

    // ─── GET /Reports/QualityData ─────────────────────────────────────────────

    /// <summary>
    /// Renders quality data analytics report: defect counts per part,
    /// inspection pass/fail rates, and NCN trends.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> QualityData(DateTime? dateFrom, DateTime? dateTo)
    {
        try
        {
            var fromDate = dateFrom ?? DateTime.Today.AddMonths(-1);
            var toDate = dateTo ?? DateTime.Today.AddDays(1);

            // Inspection summary by status
            var inspectionStats = await _db.InspectionRecords
                .AsNoTracking()
                .Where(r => r.CreatedAt >= fromDate && r.CreatedAt <= toDate)
                .GroupBy(r => r.NcnStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            // NCN defect summary by part
            var ncnByPart = await _db.NcnRecords
                .AsNoTracking()
                .Where(n => n.CreatedAt >= fromDate && n.CreatedAt <= toDate)
                .GroupBy(n => n.PartNumber)
                .Select(g => new
                {
                    PartNumber = g.Key,
                    NcnCount = g.Count(),
                    TotalDefectQty = g.Sum(n => n.DefectQty ?? 0)
                })
                .OrderByDescending(x => x.NcnCount)
                .ToListAsync();

            // NCN by defect type
            var ncnByType = await _db.NcnRecords
                .AsNoTracking()
                .Where(n => n.CreatedAt >= fromDate && n.CreatedAt <= toDate && n.DefectType != null)
                .GroupBy(n => n.DefectType)
                .Select(g => new { DefectType = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            ViewBag.InspectionStats = inspectionStats;
            ViewBag.NcnByPart = ncnByPart;
            ViewBag.NcnByType = ncnByType;
            ViewBag.DateFrom = fromDate.ToString("dd/MM/yyyy");
            ViewBag.DateTo = toDate.ToString("dd/MM/yyyy");
            ViewBag.PrintedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            ViewBag.PrintedBy = User.Identity?.Name;
            ViewBag.TotalInspections = inspectionStats.Sum(s => s.Count);
            ViewBag.TotalNcns = ncnByPart.Sum(n => n.NcnCount);

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating quality data report.");
            TempData["Error"] = "Gagal membuat laporan quality data.";
            return RedirectToAction(nameof(Index));
        }
    }
}
