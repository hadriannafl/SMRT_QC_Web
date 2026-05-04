using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using SMRT_QC_Web.Data;
using SMRT_QC_Web.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace SMRT_QC_Web.Controllers;

/// <summary>
/// Handles user authentication: login, logout, and access-denied redirect.
/// Uses cookie-based authentication with BCrypt password verification.
/// </summary>
public class AuthController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext db, ILogger<AuthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── GET /Auth/Login ──────────────────────────────────────────────────────

    /// <summary>Menampilkan halaman login. Redirect ke dashboard jika sudah login.</summary>
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel());
    }

    // ─── POST /Auth/Login ─────────────────────────────────────────────────────

    /// <summary>
    /// Validates credentials, hashes comparison via BCrypt,
    /// and issues a cookie auth ticket with role claims.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == model.UserName);

        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
        {
            ModelState.AddModelError("", "Username atau password salah.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Role, user.Position),
            new Claim("Position", user.Position),
            new Claim("UserId", user.Id.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : DateTimeOffset.UtcNow.AddHours(8)
            });

        _logger.LogInformation("User {UserName} ({Position}) logged in.", user.UserName, user.Position);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Dashboard");
    }

    // ─── POST /Auth/Logout ────────────────────────────────────────────────────

    /// <summary>Signs the user out and clears the auth cookie, then redirects to login.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userName = User.Identity?.Name;
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        _logger.LogInformation("User {UserName} logged out.", userName);
        return RedirectToAction(nameof(Login));
    }

    // ─── GET /Auth/AccessDenied ───────────────────────────────────────────────

    /// <summary>Displays access denied page when a user lacks the required role.</summary>
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
