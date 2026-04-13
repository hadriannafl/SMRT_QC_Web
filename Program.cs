using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using SMRT_QC_Web.Data;
using SMRT_QC_Web.Hubs;

/// <summary>
/// Entry point for the SmartIQC web application.
/// Configures services, middleware pipeline, authentication, EF Core, SignalR, and session.
/// </summary>
var builder = WebApplication.CreateBuilder(args);

// ─── MVC + Razor Views ───────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ─── Entity Framework Core with Pomelo MySQL provider ────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Hardcoded MariaDB 10.4.28 (XAMPP) — avoids an extra DB round-trip on startup.
// Update this version string if the MariaDB server is upgraded.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MariaDbServerVersion(new Version(10, 4, 28)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 1,
            maxRetryDelay: TimeSpan.FromSeconds(2),
            errorNumbersToAdd: null
        )
    )
);

// ─── Cookie-based Authentication ─────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.Cookie.Name = "SmartIQC.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// ─── Authorization policies per role ─────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("ADMIN"));
    options.AddPolicy("ManagerUp", policy => policy.RequireRole("ADMIN", "MANAGER"));
    options.AddPolicy("SupervisorUp", policy => policy.RequireRole("ADMIN", "MANAGER", "SUPERVISOR"));
    options.AddPolicy("AllRoles", policy => policy.RequireRole("ADMIN", "MANAGER", "SUPERVISOR", "STAFF"));
});

// ─── Session (used for flash messages) ───────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "SmartIQC.Session";
});

// ─── SignalR ─────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ─── HttpContextAccessor (for hub DI) ────────────────────────────────────────
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ─── Apply EF Core migrations on startup (dev convenience) ───────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ─── Middleware pipeline ──────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Dashboard/Error");
    app.UseHsts();
    // HTTPS redirect only in production — in development the dev cert
    // is often not trusted, causing a slow double round-trip on every request.
    app.UseHttpsRedirection();
}
else
{
    app.UseDeveloperExceptionPage();
}
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// ─── Route configuration ──────────────────────────────────────────────────────
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

// ─── SignalR hub endpoint ─────────────────────────────────────────────────────
app.MapHub<NotificationHub>("/notificationHub");

app.Run();
