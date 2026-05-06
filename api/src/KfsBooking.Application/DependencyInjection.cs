using FluentValidation;
using KfsBooking.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KfsBooking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditoriumService, AuditoriumService>();
        services.AddScoped<IBookingService, BookingService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
