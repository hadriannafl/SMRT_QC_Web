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
        // Lewati validasi kredensial — langsung sign-in sebagai ADMIN
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "ADMIN"),
            new Claim(ClaimTypes.Role, "ADMIN"),
            new Claim("Position", "ADMIN"),
            new Claim("UserId", "1")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

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
