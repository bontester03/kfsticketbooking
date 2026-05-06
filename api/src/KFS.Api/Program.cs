using KFS.Api.Hubs;
using KFS.Api.Identity;
using KFS.Api.Jobs;
using KFS.Api.Middleware;
using KFS.Application;
using KFS.Application.Interfaces;
using KFS.Infrastructure;
using KFS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF;
using QuestPDF.Infrastructure;
using Serilog;

QuestPDF.Settings.License = LicenseType.Community;

// Npgsql 6+ rejects DateTime.Kind=Unspecified by default. The codebase uses UTC consistently
// for time-of-event values, but DateOfBirth (a calendar-only field) is loaded as Unspecified.
// Enabling the legacy switch keeps that just-works without per-property column-type wrangling.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

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

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || config.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "KFS Booking API v1"));
}

// Serve QR PNGs and other generated blobs from the local-disk store.
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
