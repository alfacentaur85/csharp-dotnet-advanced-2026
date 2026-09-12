using Events.Application.Interfaces;
using Events.Application.Options;
using Events.Infrastructure.Caching;
using Events.Infrastructure.DataAccess;
using Events.Infrastructure.DataAccess.Repositories;
using Events.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Events.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        string connectionString,
        IConfiguration configuration)
    {
        services.AddDbContext<EventsDbContext>(options => options.UseNpgsql(connectionString));

        return services.AddInfrastructureCore(configuration);
    }

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext,
        IConfiguration configuration)
    {
        services.AddDbContext<EventsDbContext>(configureDbContext);

        return services.AddInfrastructureCore(configuration);
    }

    private static IServiceCollection AddInfrastructureCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IProcessedBookingEventStore, ProcessedBookingEventStore>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.Configure<KafkaConsumerOptions>(configuration.GetSection("Kafka"));
        services.Configure<CacheOptions>(configuration.GetSection("Redis"));

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var cacheOptions = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<RedisCacheService>>();

            var redisConfiguration = ConfigurationOptions.Parse(cacheOptions.ConnectionString);
            redisConfiguration.AbortOnConnectFail = false;

            try
            {
                return ConnectionMultiplexer.Connect(redisConfiguration);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize Redis connection to '{ConnectionString}'. Cache will be unavailable until Redis comes back.", cacheOptions.ConnectionString);
                throw;
            }
        });
        services.AddSingleton<ICacheService, RedisCacheService>();

        // Порядок регистрации важен: топик должен существовать до того, как потребитель начнёт на него подписываться.
        services.AddHostedService<KafkaTopicInitializerHostedService>();
        services.AddHostedService<BookingConfirmedConsumerBackgroundService>();

        return services;
    }
}
