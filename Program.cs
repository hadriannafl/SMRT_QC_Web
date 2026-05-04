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

// Use MySQL 8.x on Railway, MariaDB 10.4 locally — avoids blocking AutoDetect call at startup.
var serverVersion = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MYSQLHOST"))
    ? (ServerVersion)new MySqlServerVersion(new Version(8, 0, 0))
    : new MariaDbServerVersion(new Version(10, 4, 28));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        serverVersion,
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null
        )
    )
);

static string GetConnectionString(IConfiguration config)
{
    // Railway may expose variables as MYSQLHOST or MYSQL_HOST depending on plugin version
    var host = Environment.GetEnvironmentVariable("MYSQLHOST")
            ?? Environment.GetEnvironmentVariable("MYSQL_HOST");
    if (!string.IsNullOrEmpty(host))
    {
        var port     = Environment.GetEnvironmentVariable("MYSQLPORT")     ?? Environment.GetEnvironmentVariable("MYSQL_PORT")     ?? "3306";
        var database = Environment.GetEnvironmentVariable("MYSQLDATABASE") ?? Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "smartiqc_db";
        var user     = Environment.GetEnvironmentVariable("MYSQLUSER")     ?? Environment.GetEnvironmentVariable("MYSQL_USER")     ?? "root";
        var password = Environment.GetEnvironmentVariable("MYSQLPASSWORD") ?? Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "";
        return $"Server={host};Port={port};Database={database};User={user};Password={password};CharSet=utf8mb4;ConnectionTimeout=15;DefaultCommandTimeout=30;";
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

// ─── Apply EF Core migrations on startup ─────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (int attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            db.Database.EnsureCreated();
            SeedDemoUsers(db);
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning("DB not ready (attempt {Attempt}/10): {Message}", attempt, ex.Message);
            if (attempt == 10) throw;
            Thread.Sleep(TimeSpan.FromSeconds(attempt * 2));
        }
    }
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
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Dashboard/Error");
    // Railway terminates TLS at the proxy — skip HTTPS redirect inside the container.
    // Only redirect when not running on Railway (i.e. self-hosted with a real cert).
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT_NAME")))
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
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
