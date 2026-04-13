using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using SMRT_QC_Web.Data;
using SMRT_QC_Web.Models;

namespace SMRT_QC_Web.Controllers;

/// <summary>
/// Handles Excel import and export operations using ClosedXML.
/// Export: streams Excel files directly to the browser.
/// Import: reads uploaded .xlsx files and batch-inserts records.
/// </summary>
[Authorize(Roles = "ADMIN,MANAGER")]
public class ImportExportController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<ImportExportController> _logger;

    public ImportExportController(AppDbContext db, ILogger<ImportExportController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── GET /ImportExport/Index ──────────────────────────────────────────────

    /// <summary>Displays the import/export hub page with options for each entity.</summary>
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    // ─── GET /ImportExport/ExportInspections ─────────────────────────────────

    /// <summary>
    /// Exports all inspection records to an Excel file (.xlsx).
    /// Columns match the inspection_records table. File is streamed to browser.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ExportInspections(DateTime? dateFrom, DateTime? dateTo, string? partNumber)
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

            var records = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Inspections");

            // ── Header row ──
            var headers = new[]
            {
                "ID", "Supply Number", "Supply Date", "Line Number", "Part Number",
                "PO Number", "Operator", "Item Category", "NCN No",
                "Checker", "Approver", "Validasi", "Status", "Created At"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A237E");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // ── Data rows ──
            for (int row = 0; row < records.Count; row++)
            {
                var r = records[row];
                var excelRow = row + 2;
                ws.Cell(excelRow, 1).Value = r.Id;
                ws.Cell(excelRow, 2).Value = r.SupplyNumber;
                ws.Cell(excelRow, 3).Value = r.SupplyDate?.ToString("yyyy-MM-dd") ?? "";
                ws.Cell(excelRow, 4).Value = r.LineNumber ?? "";
                ws.Cell(excelRow, 5).Value = r.PartNumber ?? "";
                ws.Cell(excelRow, 6).Value = r.PoNumber ?? "";
                ws.Cell(excelRow, 7).Value = r.Operator ?? "";
                ws.Cell(excelRow, 8).Value = r.ItemCategory ?? "";
                ws.Cell(excelRow, 9).Value = r.NcnNo ?? "";
                ws.Cell(excelRow, 10).Value = r.ChekName ?? "";
                ws.Cell(excelRow, 11).Value = r.AppvName ?? "";
                ws.Cell(excelRow, 12).Value = r.Validasi ?? "";
                ws.Cell(excelRow, 13).Value = r.NcnStatus ?? "";
                ws.Cell(excelRow, 14).Value = r.CreatedAt.ToString("yyyy-MM-dd HH:mm");

                // Alternate row shading
                if (row % 2 == 1)
                    ws.Row(excelRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F6FA");
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Seek(0, SeekOrigin.Begin);

            var fileName = $"SmartIQC_Inspections_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            _logger.LogInformation("Exported {Count} inspection records.", records.Count);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting inspections.");
            TempData["Error"] = "Gagal mengekspor data inspeksi: " + ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    // ─── GET /ImportExport/ExportNcn ─────────────────────────────────────────

    /// <summary>Exports all NCN records to Excel.</summary>
    [HttpGet]
    public async Task<IActionResult> ExportNcn(DateTime? dateFrom, DateTime? dateTo)
    {
        try
        {
            var query = _db.NcnRecords.AsNoTracking();

            if (dateFrom.HasValue) query = query.Where(n => n.CreatedAt >= dateFrom.Value);
            if (dateTo.HasValue) query = query.Where(n => n.CreatedAt <= dateTo.Value.AddDays(1));

            var records = await query.OrderByDescending(n => n.CreatedAt).ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("NCN Records");

            var headers = new[]
            {
                "ID", "NCN No", "Supply Number", "Part Number", "Defect Type",
                "Defect Qty", "Checker", "Approver", "Status", "Notes", "Created At"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A237E");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (int row = 0; row < records.Count; row++)
            {
                var n = records[row];
                var excelRow = row + 2;
                ws.Cell(excelRow, 1).Value = n.Id;
                ws.Cell(excelRow, 2).Value = n.NcnNo;
                ws.Cell(excelRow, 3).Value = n.SupplyNumber ?? "";
                ws.Cell(excelRow, 4).Value = n.PartNumber ?? "";
                ws.Cell(excelRow, 5).Value = n.DefectType ?? "";
                ws.Cell(excelRow, 6).Value = n.DefectQty ?? 0;
                ws.Cell(excelRow, 7).Value = n.CheckerName ?? "";
                ws.Cell(excelRow, 8).Value = n.ApproverName ?? "";
                ws.Cell(excelRow, 9).Value = n.Status;
                ws.Cell(excelRow, 10).Value = n.Notes ?? "";
                ws.Cell(excelRow, 11).Value = n.CreatedAt.ToString("yyyy-MM-dd HH:mm");

                if (row % 2 == 1)
                    ws.Row(excelRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F6FA");
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var fileName = $"SmartIQC_NCN_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting NCN records.");
            TempData["Error"] = "Gagal mengekspor data NCN.";
            return RedirectToAction(nameof(Index));
        }
    }

    // ─── GET /ImportExport/ExportParts ───────────────────────────────────────

    /// <summary>Exports all parts master data to Excel.</summary>
    [HttpGet]
    public async Task<IActionResult> ExportParts()
    {
        try
        {
            var parts = await _db.Parts.AsNoTracking().OrderBy(p => p.PartNumber).ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Parts");

            var headers = new[] { "ID", "Part Number", "Part Name", "Category", "Spec", "Created At" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A237E");
                cell.Style.Font.FontColor = XLColor.White;
            }

            for (int row = 0; row < parts.Count; row++)
            {
                var p = parts[row];
                var excelRow = row + 2;
                ws.Cell(excelRow, 1).Value = p.Id;
                ws.Cell(excelRow, 2).Value = p.PartNumber;
                ws.Cell(excelRow, 3).Value = p.PartName;
                ws.Cell(excelRow, 4).Value = p.Category ?? "";
                ws.Cell(excelRow, 5).Value = p.Spec ?? "";
                ws.Cell(excelRow, 6).Value = p.CreatedAt.ToString("yyyy-MM-dd");
                if (row % 2 == 1)
                    ws.Row(excelRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F6FA");
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"SmartIQC_Parts_{DateTime.Now:yyyyMMdd}.xlsx");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting parts.");
            TempData["Error"] = "Gagal mengekspor data part.";
            return RedirectToAction(nameof(Index));
        }
    }

    // ─── POST /ImportExport/ImportParts ──────────────────────────────────────

    /// <summary>
    /// Imports parts from an uploaded .xlsx file.
    /// Expected columns: PartNumber, PartName, Category, Spec.
    /// Skips rows with duplicate PartNumber (no overwrite on import).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportParts(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return Json(new { success = false, message = "File tidak boleh kosong." });

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Json(new { success = false, message = "Hanya file .xlsx yang didukung." });

        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Seek(0, SeekOrigin.Begin);

            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheet(1);
            var rows = ws.RowsUsed().Skip(1).ToList(); // skip header row

            int imported = 0, skipped = 0;
            var errors = new List<string>();

            foreach (var row in rows)
            {
                try
                {
                    var partNumber = row.Cell(1).GetString().Trim().ToUpper();
                    var partName = row.Cell(2).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(partNumber) || string.IsNullOrWhiteSpace(partName))
                    {
                        skipped++;
                        continue;
                    }

                    // Skip duplicates
                    var exists = await _db.Parts.AnyAsync(p => p.PartNumber == partNumber);
                    if (exists)
                    {
                        skipped++;
                        continue;
                    }

                    _db.Parts.Add(new Part
                    {
                        PartNumber = partNumber,
                        PartName = partName,
                        Category = row.Cell(3).GetString().Trim().NullIfEmpty(),
                        Spec = row.Cell(4).GetString().Trim().NullIfEmpty(),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                    imported++;
                }
                catch (Exception rowEx)
                {
                    errors.Add($"Row {row.RowNumber()}: {rowEx.Message}");
                }
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation("Parts import: {Imported} imported, {Skipped} skipped.", imported, skipped);

            return Json(new
            {
                success = true,
                message = $"Import selesai: {imported} berhasil, {skipped} dilewati.",
                imported,
                skipped,
                errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing parts.");
            return Json(new { success = false, message = "Gagal mengimpor file: " + ex.Message });
        }
    }

    // ─── POST /ImportExport/ImportInspections ─────────────────────────────────

    /// <summary>
    /// Imports inspection records from an .xlsx file.
    /// Expected columns: SupplyNumber, SupplyDate, LineNumber, PartNumber,
    /// PoNumber, Operator, ItemCategory.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportInspections(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return Json(new { success = false, message = "File tidak boleh kosong." });

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Json(new { success = false, message = "Hanya file .xlsx yang didukung." });

        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Seek(0, SeekOrigin.Begin);

            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheet(1);
            var rows = ws.RowsUsed().Skip(1).ToList();

            int imported = 0, skipped = 0;
            var errors = new List<string>();

            foreach (var row in rows)
            {
                try
                {
                    var supplyNumber = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(supplyNumber))
                    {
                        skipped++;
                        continue;
                    }

                    DateTime? supplyDate = null;
                    var dateStr = row.Cell(2).GetString().Trim();
                    if (!string.IsNullOrWhiteSpace(dateStr) && DateTime.TryParse(dateStr, out var parsedDate))
                        supplyDate = parsedDate;

                    _db.InspectionRecords.Add(new InspectionRecord
                    {
                        SupplyNumber = supplyNumber,
                        SupplyDate = supplyDate,
                        LineNumber = row.Cell(3).GetString().Trim().NullIfEmpty(),
                        PartNumber = row.Cell(4).GetString().Trim().ToUpper().NullIfEmpty(),
                        PoNumber = row.Cell(5).GetString().Trim().NullIfEmpty(),
                        Operator = row.Cell(6).GetString().Trim().NullIfEmpty(),
                        ItemCategory = row.Cell(7).GetString().Trim().NullIfEmpty(),
                        NcnStatus = "CHECK",
                        ChekName = User.Identity?.Name,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                    imported++;
                }
                catch (Exception rowEx)
                {
                    errors.Add($"Row {row.RowNumber()}: {rowEx.Message}");
                }
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation("Inspections import: {Imported} imported.", imported);
            return Json(new
            {
                success = true,
                message = $"Import selesai: {imported} berhasil, {skipped} dilewati.",
                imported,
                skipped,
                errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing inspections.");
            return Json(new { success = false, message = "Gagal mengimpor file: " + ex.Message });
        }
    }

    // ─── GET /ImportExport/DownloadTemplate ───────────────────────────────────

    /// <summary>Downloads an empty Excel template with correct column headers for import.</summary>
    [HttpGet]
    public IActionResult DownloadTemplate(string type)
    {
        try
        {
            using var workbook = new XLWorkbook();
            string fileName;

            if (type == "parts")
            {
                var ws = workbook.Worksheets.Add("Parts Template");
                var headers = new[] { "PartNumber (*)", "PartName (*)", "Category", "Spec" };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                    ws.Cell(1, i + 1).Style.Font.Bold = true;
                    ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1A237E");
                    ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                }
                // Sample row
                ws.Cell(2, 1).Value = "PN-001";
                ws.Cell(2, 2).Value = "Contoh Part";
                ws.Cell(2, 3).Value = "Kategori A";
                ws.Cell(2, 4).Value = "Spesifikasi contoh";
                ws.Columns().AdjustToContents();
                fileName = "Template_Import_Parts.xlsx";
            }
            else // inspections
            {
                var ws = workbook.Worksheets.Add("Inspections Template");
                var headers = new[]
                {
                    "SupplyNumber (*)", "SupplyDate (yyyy-MM-dd)", "LineNumber",
                    "PartNumber", "PoNumber", "Operator", "ItemCategory"
                };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                    ws.Cell(1, i + 1).Style.Font.Bold = true;
                    ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1A237E");
                    ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
                }
                ws.Cell(2, 1).Value = "SPL-20240101-001";
                ws.Cell(2, 2).Value = DateTime.Today.ToString("yyyy-MM-dd");
                ws.Cell(2, 3).Value = "LINE-1";
                ws.Cell(2, 4).Value = "PN-001";
                ws.Cell(2, 5).Value = "PO-001";
                ws.Cell(2, 6).Value = "Operator A";
                ws.Cell(2, 7).Value = "Category A";
                ws.Columns().AdjustToContents();
                fileName = "Template_Import_Inspections.xlsx";
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating template.");
            TempData["Error"] = "Gagal membuat template.";
            return RedirectToAction(nameof(Index));
        }
    }
}

/// <summary>Extension method to convert empty strings to null (used for optional import fields).</summary>
internal static class StringExtensions
{
    public static string? NullIfEmpty(this string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
