using Events.Application.DependencyInjection;
using Events.Infrastructure.DataAccess;
using Events.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Events.Tests;

public abstract class TestDiFixture : IDisposable
{
    protected readonly string DbName = Guid.NewGuid().ToString();
    protected readonly ServiceProvider ServiceProvider;

    protected TestDiFixture()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Kafka:ConsumerGroup"] = "events-service-group-tests"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddInfrastructureServices(options => options.UseInMemoryDatabase(DbName), configuration);
        services.AddApplicationServices();

        ServiceProvider = services.BuildServiceProvider();

        // создаём БД один раз на класс
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        ServiceProvider.Dispose();
    }
}
