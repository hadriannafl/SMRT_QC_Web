using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SMRT_QC_Web.Data;
using SMRT_QC_Web.Models;

namespace SMRT_QC_Web.Controllers;

/// <summary>
/// CRUD management for system users. Restricted to ADMIN role only.
/// Passwords are hashed using BCrypt before persistence.
/// </summary>
[Authorize(Roles = "ADMIN")]
public class UserController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<UserController> _logger;

    // Available positions/roles for dropdown selection
    private static readonly string[] Positions = { "ADMIN", "MANAGER", "SUPERVISOR", "STAFF" };

    public UserController(AppDbContext db, ILogger<UserController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── GET /Users/Index ─────────────────────────────────────────────────────

    /// <summary>Displays paginated list of all users with optional search filter.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        const int pageSize = 20;
        ViewBag.Positions = Positions;
        ViewBag.Search = search;

        try
        {
            var query = _db.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u => u.UserName.Contains(search) || u.Position.Contains(search));

            var total = await query.CountAsync();
            var users = await query
                .OrderBy(u => u.UserName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.TotalRecords = total;

            return View(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users list.");
            TempData["Error"] = "Gagal memuat data pengguna.";
            return View(new List<User>());
        }
    }

    // ─── POST /Users/Create ───────────────────────────────────────────────────

    /// <summary>
    /// Creates a new user. The plain-text password from the form is
    /// hashed with BCrypt (work factor 12) before saving to the database.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string userName, string password, string position)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
                return Json(new { success = false, message = "Username dan password wajib diisi." });

            if (!Positions.Contains(position))
                return Json(new { success = false, message = "Posisi tidak valid." });

            // Check uniqueness
            var exists = await _db.Users.AnyAsync(u => u.UserName == userName);
            if (exists)
                return Json(new { success = false, message = "Username sudah digunakan." });

            var user = new User
            {
                UserName = userName.Trim(),
                Password = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
                Position = position,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("New user created: {UserName} ({Position})", user.UserName, user.Position);
            return Json(new { success = true, message = "Pengguna berhasil dibuat." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {UserName}.", userName);
            return Json(new { success = false, message = "Gagal membuat pengguna: " + ex.Message });
        }
    }

    // ─── GET /Users/GetUser (AJAX) ────────────────────────────────────────────

    /// <summary>Returns user data as JSON for populating the edit modal.</summary>
    [HttpGet]
    public async Task<IActionResult> GetUser(int id)
    {
        try
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return Json(new { success = false, message = "Pengguna tidak ditemukan." });

            return Json(new
            {
                success = true,
                data = new
                {
                    user.Id,
                    user.UserName,
                    user.Position,
                    CreatedAt = user.CreatedAt.ToString("dd/MM/yyyy")
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user {Id}.", id);
            return Json(new { success = false, message = "Gagal memuat data pengguna." });
        }
    }

    // ─── POST /Users/Edit ─────────────────────────────────────────────────────

    /// <summary>
    /// Updates user data. If a new password is provided it is re-hashed;
    /// if password field is empty, the existing hash is retained.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string userName, string? newPassword, string position)
    {
        try
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return Json(new { success = false, message = "Pengguna tidak ditemukan." });

            if (!Positions.Contains(position))
                return Json(new { success = false, message = "Posisi tidak valid." });

            // Check uniqueness for the new username (exclude current record)
            var conflict = await _db.Users.AnyAsync(u => u.UserName == userName && u.Id != id);
            if (conflict)
                return Json(new { success = false, message = "Username sudah digunakan oleh pengguna lain." });

            user.UserName = userName.Trim();
            user.Position = position;
            user.UpdatedAt = DateTime.Now;

            // Only update password if a new one was supplied
            if (!string.IsNullOrWhiteSpace(newPassword))
                user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 12);

            await _db.SaveChangesAsync();

            _logger.LogInformation("User {Id} updated: {UserName} ({Position})", id, user.UserName, user.Position);
            return Json(new { success = true, message = "Data pengguna berhasil diperbarui." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {Id}.", id);
            return Json(new { success = false, message = "Gagal memperbarui pengguna: " + ex.Message });
        }
    }

    // ─── POST /Users/Delete ───────────────────────────────────────────────────

    /// <summary>
    /// Deletes a user. Prevents deletion of the currently logged-in user
    /// to avoid locking out the only admin.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            // Prevent self-deletion
            var currentUserId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
            if (id == currentUserId)
                return Json(new { success = false, message = "Tidak dapat menghapus akun yang sedang login." });

            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return Json(new { success = false, message = "Pengguna tidak ditemukan." });

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("User {Id} ({UserName}) deleted.", id, user.UserName);
            return Json(new { success = true, message = "Pengguna berhasil dihapus." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {Id}.", id);
            return Json(new { success = false, message = "Gagal menghapus pengguna: " + ex.Message });
        }
    }
}
