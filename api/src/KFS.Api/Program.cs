using KFS.Api.Hubs;
using KFS.Api.Identity;
using KFS.Api.Jobs;
using KFS.Api.Middleware;
using KFS.Application;
using KFS.Application.Interfaces;
using KFS.Infrastructure;
using KFS.Infrastructure.Persistence;
using Microsoft.OpenApi.Models;
using QuestPDF;
using QuestPDF.Infrastructure;
using Serilog;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

NormalizeConnectionString(builder);
NormalizePort(builder);

builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/kfs-.log", rollingInterval: RollingInterval.Day));

var config = builder.Configuration;

// Application Insights — connection string injected as an env var on Azure App Service.
// Locally absent: AddApplicationInsightsTelemetry no-ops cleanly.
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddInfrastructure(config);
builder.Services.AddApplication();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ISeatNotifier, SignalRSeatNotifier>();

builder.Services.AddSignalR();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "KFS Booking API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Bearer JWT", Name = "Authorization",
        In = ParameterLocation.Header, Type = SecuritySchemeType.ApiKey, Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

builder.Services.AddHealthChecks();

const string corsPolicy = "kfs-cors";
builder.Services.AddCors(options => options.AddPolicy(corsPolicy, p =>
{
    var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? new[] { "http://localhost:5173", "http://localhost:5174", "http://localhost:5175" };
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

builder.Services.AddHostedService<CartSweeperJob>();
builder.Services.AddHostedService<RebookWindowJob>();
builder.Services.AddHostedService<DayBeforeReminderJob>();
// Pre-warms BCrypt + EF JIT off the request thread so the first login burst is fast.
builder.Services.AddHostedService<StartupWarmer>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || config.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "KFS Booking API v1"));
}

// Local-disk fallback for QR PNGs when Storage:Provider is LocalDisk. On Azure with
// Storage:Provider=AzureBlob the API never serves blobs directly — clients fetch via SAS URL.
var staticRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
Directory.CreateDirectory(staticRoot);
app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(staticRoot),
    RequestPath = "/static"
});

app.UseCors(corsPolicy);
app.UseAuthentication();
app.UseAuthorization();

// /healthz is the App Service / Static Web App / Azure Front Door probe path. Cheap, no DB hit.
app.MapHealthChecks("/healthz");

app.MapControllers();
app.MapHub<SeatMapHub>("/hubs/seatmap");

if (config.GetValue<bool>("Database:RunMigrationsOnStartup", true))
{
    await DbSeeder.SeedAsync(app.Services);
}

app.Run();

static void NormalizePort(WebApplicationBuilder builder)
{
    var port = Environment.GetEnvironmentVariable("PORT");
    if (string.IsNullOrWhiteSpace(port)) return;
    builder.WebHost.UseUrls($"http://+:{port}");
}

// Translate Railway/Heroku-style DATABASE_URL (postgres://user:pass@host:port/db) into the
// Npgsql key=value format. If DATABASE_URL is absent, the appsettings value passes through.
// On Azure App Service the connection string is injected via Key Vault reference into
// ConnectionStrings__Default, so neither branch runs in production — but it costs nothing
// to support both formats and keeps local dev consistent with cloud-style env vars.
static void NormalizeConnectionString(WebApplicationBuilder builder)
{
    var raw = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (string.IsNullOrWhiteSpace(raw))
    {
        var existing = builder.Configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(existing)) return;
        if (!existing.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !existing.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return;
        raw = existing;
    }

    var uri = new Uri(raw);
    var userInfo = uri.UserInfo.Split(':', 2);
    var csb = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port == -1 ? 5432 : uri.Port,
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        Database = uri.AbsolutePath.TrimStart('/'),
        SslMode = Npgsql.SslMode.Prefer
    };
    builder.Configuration["ConnectionStrings:Default"] = csb.ConnectionString;
}

public partial class Program { }
