using Bookings.Application.Interfaces;
using Bookings.Infrastructure.BackgroundServices;
using Bookings.Infrastructure.DataAccess;
using Bookings.Infrastructure.DataAccess.Repositories;
using Bookings.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookings.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        string connectionString,
        IConfiguration configuration)
    {
        services.AddDbContext<BookingsDbContext>(options => options.UseNpgsql(connectionString));
        services.Configure<KafkaProducerOptions>(configuration.GetSection("Kafka"));

        return services.AddInfrastructureCore();
    }

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContext<BookingsDbContext>(configureDbContext);

        return services.AddInfrastructureCore();
    }

    private static IServiceCollection AddInfrastructureCore(this IServiceCollection services)
    {
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Singleton: один и тот же Kafka-продюсер на всё время жизни приложения.
        services.AddSingleton<KafkaBookingConfirmedPublisher>();
        services.AddSingleton<IBookingConfirmedPublisher>(sp => sp.GetRequiredService<KafkaBookingConfirmedPublisher>());

        services.AddHostedService<BookingProcessingBackgroundService>();

        return services;
    }
}
