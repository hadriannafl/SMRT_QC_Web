using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SMRT_QC_Web.Data;
using SMRT_QC_Web.Models.ViewModels;

namespace SMRT_QC_Web.Controllers;

/// <summary>
/// Dashboard controller: aggregates KPI stats, recent activity, and
/// provides a JSON endpoint polled by SignalR client for notifications.
/// </summary>
[Authorize]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(AppDbContext db, ILogger<DashboardController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── GET /Dashboard/Index ─────────────────────────────────────────────────

    /// <summary>
    /// Renders the main dashboard with stat cards, recent inspections table,
    /// and unread notification panel for the current user's role.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            // Cek koneksi DB terlebih dahulu — jika gagal langsung tampilkan dashboard kosong
            // agar tidak menunggu retry timeout yang lama
            var canConnect = await _db.Database.CanConnectAsync();
            if (!canConnect)
            {
                _logger.LogWarning("Database tidak tersedia, dashboard ditampilkan kosong.");
                return View(new DashboardViewModel());
            }

            var userRole = User.FindFirst("Position")?.Value ?? "STAFF";

            var vm = new DashboardViewModel
            {
                TotalInspections = await _db.InspectionRecords
                    .AsNoTracking().CountAsync(),

                PendingInspections = await _db.InspectionRecords
                    .AsNoTracking().CountAsync(r => r.NcnStatus == "CHECK"),

                PendingValidation = await _db.InspectionRecords
                    .AsNoTracking().CountAsync(r => r.NcnStatus == "VALIDATION"),

                OpenNcns = await _db.NcnRecords
                    .AsNoTracking().CountAsync(n => n.Status != "DONE"),

                TotalNcns = await _db.NcnRecords
                    .AsNoTracking().CountAsync(),

                TotalParts = await _db.Parts
                    .AsNoTracking().CountAsync(),

                UnreadNotifications = await _db.Notifications
                    .AsNoTracking()
                    .CountAsync(n => !n.IsRead && (n.Position == userRole || n.Position == "ALL")),

                RecentInspections = await _db.InspectionRecords
                    .AsNoTracking()
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(10)
                    .ToListAsync(),

                Notifications = await _db.Notifications
                    .AsNoTracking()
                    .Where(n => !n.IsRead && (n.Position == userRole || n.Position == "ALL"))
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(10)
                    .ToListAsync(),

                RecentNcns = await _db.NcnRecords
                    .AsNoTracking()
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(5)
                    .ToListAsync(),
            };

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard.");
            return View(new DashboardViewModel());
        }
    }

    // ─── GET /Dashboard/Notifications (AJAX) ──────────────────────────────────

    /// <summary>
    /// Returns unread notifications as JSON for the SignalR client to
    /// update the notification badge/dropdown without full page reload.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Notifications()
    {
        try
        {
            var userRole = User.FindFirst("Position")?.Value ?? "STAFF";

            var notifications = await _db.Notifications
                .Where(n => !n.IsRead && (n.Position == userRole || n.Position == "ALL"))
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .Select(n => new
                {
                    n.Id,
                    n.SuppNumber,
                    n.PartNumber,
                    n.Notif,
                    n.IsRead,
                    CreatedAt = n.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                    TimeAgo = n.TimeAgo
                })
                .ToListAsync();

            var unreadCount = await _db.Notifications
                .CountAsync(n => !n.IsRead && (n.Position == userRole || n.Position == "ALL"));

            return Json(new { success = true, data = notifications, unreadCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notifications.");
            return Json(new { success = false, message = "Gagal memuat notifikasi." });
        }
    }

    // ─── POST /Dashboard/MarkRead ─────────────────────────────────────────────

    /// <summary>Marks a single notification as read (called via AJAX from notification dropdown).</summary>
    [HttpPost]
    public async Task<IActionResult> MarkRead(int id)
    {
        try
        {
            var notif = await _db.Notifications.FindAsync(id);
            if (notif == null)
                return Json(new { success = false, message = "Notifikasi tidak ditemukan." });

            notif.IsRead = true;
            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {Id} as read.", id);
            return Json(new { success = false, message = "Gagal memperbarui notifikasi." });
        }
    }

    // ─── POST /Dashboard/MarkAllRead ─────────────────────────────────────────

    /// <summary>Marks all unread notifications for the current role as read.</summary>
    [HttpPost]
    public async Task<IActionResult> MarkAllRead()
    {
        try
        {
            var userRole = User.FindFirst("Position")?.Value ?? "STAFF";
            var unread = await _db.Notifications
                .Where(n => !n.IsRead && (n.Position == userRole || n.Position == "ALL"))
                .ToListAsync();

            unread.ForEach(n => n.IsRead = true);
            await _db.SaveChangesAsync();
            return Json(new { success = true, count = unread.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read.");
            return Json(new { success = false, message = "Gagal memperbarui notifikasi." });
        }
    }

    // ─── GET /Dashboard/Error ─────────────────────────────────────────────────

    /// <summary>Generic error page shown when an unhandled exception propagates.</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View();
    }
}
