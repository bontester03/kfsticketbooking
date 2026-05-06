using KfsBooking.Api.Identity;
using KfsBooking.Api.Middleware;
using KfsBooking.Application;
using KfsBooking.Application.Interfaces;
using KfsBooking.Infrastructure;
using KfsBooking.Infrastructure.Persistence;
using Microsoft.OpenApi.Models;
using Npgsql;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

NormalizeConnectionString(builder);
NormalizePort(builder);

builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/kfsbooking-.log", rollingInterval: RollingInterval.Day));

var config = builder.Configuration;

builder.Services.AddInfrastructure(config);
builder.Services.AddApplication();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

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
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var corsPolicy = "kfsbooking-cors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicy, p =>
    {
        var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:4200" };
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || config.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "KFS Booking API v1"));
}

app.UseCors(corsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (config.GetValue<bool>("Database:RunMigrationsOnStartup", true))
{
    await DbSeeder.SeedAsync(app.Services);
}

app.Run();

// Normalizes a Railway/Heroku-style postgres URL (postgres://user:pass@host:port/db) into the
// Npgsql key=value format. If DATABASE_URL is unset or already key=value, leaves config alone.
static void NormalizeConnectionString(WebApplicationBuilder builder)
{
    var raw = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (string.IsNullOrWhiteSpace(raw))
    {
        var existing = builder.Configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(existing)) return;
        if (existing.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || existing.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            raw = existing;
        else
            return;
    }

    var uri = new Uri(raw);
    var userInfo = uri.UserInfo.Split(':', 2);
    var csb = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port == -1 ? 5432 : uri.Port,
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        Database = uri.AbsolutePath.TrimStart('/'),
        SslMode = SslMode.Prefer,
        TrustServerCertificate = true
    };

    builder.Configuration["ConnectionStrings:Default"] = csb.ConnectionString;
}

// Railway / Heroku / Cloud Run inject the listen port via PORT. Honor it without forcing the
// operator to also set ASPNETCORE_URLS.
static void NormalizePort(WebApplicationBuilder builder)
{
    var port = Environment.GetEnvironmentVariable("PORT");
    if (string.IsNullOrWhiteSpace(port)) return;
    builder.WebHost.UseUrls($"http://+:{port}");
}

public partial class Program { }
