using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using SMRT_QC_Web.Data;
using SMRT_QC_Web.Hubs;
using SMRT_QC_Web.Models;

var builder = WebApplication.CreateBuilder(args);

// Railway sets PORT env var; bind to 0.0.0.0 so the container is reachable.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5050";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ─── MVC + Razor Views ───────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ─── Entity Framework Core with Pomelo MySQL provider ────────────────────────
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

// DB init runs in background so HTTP server starts immediately (Railway healthcheck friendly)
builder.Services.AddHostedService<DbInitializerService>();

static string GetConnectionString(IConfiguration config)
{
    var host = Environment.GetEnvironmentVariable("MYSQLHOST")
            ?? Environment.GetEnvironmentVariable("MYSQL_HOST");
    if (!string.IsNullOrEmpty(host))
    {
        var p        = Environment.GetEnvironmentVariable("MYSQLPORT")     ?? Environment.GetEnvironmentVariable("MYSQL_PORT")     ?? "3306";
        var database = Environment.GetEnvironmentVariable("MYSQLDATABASE") ?? Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "smartiqc_db";
        var user     = Environment.GetEnvironmentVariable("MYSQLUSER")     ?? Environment.GetEnvironmentVariable("MYSQL_USER")     ?? "root";
        var password = Environment.GetEnvironmentVariable("MYSQLPASSWORD") ?? Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "";
        return $"Server={host};Port={p};Database={database};User={user};Password={password};CharSet=utf8mb4;ConnectionTimeout=15;DefaultCommandTimeout=30;";
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

// ─── Middleware pipeline ──────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Dashboard/Error");
    // Railway terminates TLS at the proxy — skip HTTPS redirect inside the container.
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.MapHub<NotificationHub>("/notificationHub");

app.Run();

// ─── Background service: DB init + seed (runs after HTTP server starts) ──────
public class DbInitializerService(IServiceProvider services, ILogger<DbInitializerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Small delay so the HTTP server is fully up before we hit the DB
        await Task.Delay(TimeSpan.FromSeconds(3), ct);

        for (int attempt = 1; attempt <= 20 && !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
                SeedDemoUsers(db);
                logger.LogInformation("Database ready.");
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("DB not ready (attempt {A}/20): {M}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt * 3, 30)), ct);
            }
        }
    }

    private static void SeedDemoUsers(AppDbContext db)
    {
        var demoUsers = new[]
        {
            new { UserName = "demo_admin",      Position = "ADMIN" },
            new { UserName = "demo_manager",    Position = "MANAGER" },
            new { UserName = "demo_supervisor", Position = "SUPERVISOR" },
            new { UserName = "demo_staff",      Position = "STAFF" },
        };

        var existing = db.Users
            .Where(u => u.UserName.StartsWith("demo_"))
            .Select(u => u.UserName)
            .ToHashSet();

        var hash = BCrypt.Net.BCrypt.HashPassword("demo123", workFactor: 12);
        var now  = DateTime.Now;

        foreach (var d in demoUsers)
        {
            if (existing.Contains(d.UserName)) continue;
            db.Users.Add(new User
            {
                UserName  = d.UserName,
                Password  = hash,
                Position  = d.Position,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        db.SaveChanges();
    }
}
