using FluentValidation;
using KFS.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KFS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<ISeatMapService, SeatMapService>();
        services.AddScoped<IAdminPassService, AdminPassService>();
        services.AddScoped<IScannerService, ScannerService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IReminderService, ReminderService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
