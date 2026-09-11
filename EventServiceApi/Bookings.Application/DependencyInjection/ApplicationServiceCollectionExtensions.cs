using Bookings.Application.Interfaces;
using Bookings.Application.Options;
using Bookings.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookings.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddOptions<BookingOptions>();

        services.AddScoped<IBookingService, BookingService>();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApplicationServices();
        services.Configure<BookingOptions>(configuration.GetSection("Bookings"));

        return services;
    }
}
