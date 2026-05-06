using System.Text;
using KFS.Application.Interfaces;
using KFS.Infrastructure.Email;
using KFS.Infrastructure.Excel;
using KFS.Infrastructure.Identity;
using KFS.Infrastructure.Pdf;
using KFS.Infrastructure.Persistence;
using KFS.Infrastructure.Qr;
using KFS.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace KFS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

        services.AddDbContext<KfsDbContext>(opt => opt
            .UseNpgsql(connectionString, npg =>
                npg.MigrationsAssembly(typeof(KfsDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<KfsDbContext>());

        services.Configure<JwtSettings>(config.GetSection(JwtSettings.SectionName));
        services.Configure<QrSettings>(config.GetSection(QrSettings.SectionName));

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IQrCodeService, QrCodeService>();
        services.AddScoped<IExcelStudentImporter, ExcelStudentImporter>();
        services.AddScoped<IPassPdfRenderer, PassPdfRenderer>();
        services.AddScoped<ITicketEmailRenderer, TicketEmailRenderer>();

        // Storage backend: AzureBlob (prod / Azurite local) or LocalDisk fallback.
        var storageProvider = config.GetValue<string>("Storage:Provider") ?? "LocalDisk";
        if (string.Equals(storageProvider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IBlobStorage, AzureBlobStorage>();
        else
            services.AddSingleton<IBlobStorage, LocalDiskBlobStorage>();

        // Email — Console renderer by default; SendGrid/ACS swap-in lives in API layer.
        services.AddScoped<IEmailService, ConsoleEmailService>();

        var jwtSection = config.GetSection(JwtSettings.SectionName);
        var secret = jwtSection.GetValue<string>("Secret")
            ?? throw new InvalidOperationException("Jwt:Secret is required.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSection.GetValue<string>("Issuer"),
                    ValidAudience = jwtSection.GetValue<string>("Audience"),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
