using EventService.Application.Interfaces;
using EventService.Application.Options;
using EventService.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EventServiceImpl = EventService.Application.Services.EventService;

namespace EventService.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddOptions<BookingOptions>();

        services.AddScoped<IEventService, EventServiceImpl>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApplicationServices();
        services.Configure<BookingOptions>(configuration.GetSection("Bookings"));

        return services;
    }
}
