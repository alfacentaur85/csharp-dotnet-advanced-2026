using EventService.Application.Interfaces;
using EventService.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using EventServiceImpl = EventService.Application.Services.EventService;

namespace EventService.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventServiceImpl>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
