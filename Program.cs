using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using SMRT_QC_Web.Data;
using SMRT_QC_Web.Hubs;

/// <summary>
/// Entry point for the SmartIQC web application.
/// Configures services, middleware pipeline, authentication, EF Core, SignalR, and session.
/// </summary>
var builder = WebApplication.CreateBuilder(args);

// Railway sets PORT env var; bind to 0.0.0.0 so the container is reachable.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5050";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ─── MVC + Razor Views ───────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ─── Entity Framework Core with Pomelo MySQL provider ────────────────────────
// Railway injects MYSQLHOST/MYSQLPORT/MYSQLDATABASE/MYSQLUSER/MYSQLPASSWORD;
// fall back to DefaultConnection for local development.
var connectionString = GetConnectionString(builder.Configuration);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null
        )
    )
);

static string GetConnectionString(IConfiguration config)
{
    var host = Environment.GetEnvironmentVariable("MYSQLHOST");
    if (!string.IsNullOrEmpty(host))
    {
        var port     = Environment.GetEnvironmentVariable("MYSQLPORT")     ?? "3306";
        var database = Environment.GetEnvironmentVariable("MYSQLDATABASE") ?? "smartiqc_db";
        var user     = Environment.GetEnvironmentVariable("MYSQLUSER")     ?? "root";
        var password = Environment.GetEnvironmentVariable("MYSQLPASSWORD") ?? "";
        return $"Server={host};Port={port};Database={database};User={user};Password={password};CharSet=utf8mb4;ConnectionTimeout=10;DefaultCommandTimeout=30;";
    }
    return config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}

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
    SeedDemoUsers(db);
}

static void SeedDemoUsers(AppDbContext db)
{
    var demoUsers = new[]
    {
        new { UserName = "demo_admin",      Position = "ADMIN" },
        new { UserName = "demo_manager",    Position = "MANAGER" },
        new { UserName = "demo_supervisor", Position = "SUPERVISOR" },
        new { UserName = "demo_staff",      Position = "STAFF" },
    };

    var existingNames = db.Users
        .Where(u => u.UserName.StartsWith("demo_"))
        .Select(u => u.UserName)
        .ToHashSet();

    var passwordHash = BCrypt.Net.BCrypt.HashPassword("demo123", workFactor: 12);
    var now = DateTime.Now;

    foreach (var d in demoUsers)
    {
        if (existingNames.Contains(d.UserName)) continue;

        db.Users.Add(new SMRT_QC_Web.Models.User
        {
            UserName  = d.UserName,
            Password  = passwordHash,
            Position  = d.Position,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    db.SaveChanges();
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
