using EventService.Application.Interfaces;
using EventService.Infrastructure.BackgroundServices;
using EventService.Infrastructure.DataAccess;
using EventService.Infrastructure.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventService.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        return services.AddInfrastructureCore();
    }

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContext<AppDbContext>(configureDbContext);

        return services.AddInfrastructureCore();
    }

    private static IServiceCollection AddInfrastructureCore(this IServiceCollection services)
    {
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddHostedService<BookingProcessingBackgroundService>();

        return services;
    }
}
