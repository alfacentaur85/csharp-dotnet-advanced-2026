using EventService.Application.DependencyInjection;
using EventService.Infrastructure.DataAccess;
using EventService.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventServiceApi.Tests;

public abstract class TestDiFixture : IDisposable
{
    protected readonly string DbName = Guid.NewGuid().ToString();
    protected readonly ServiceProvider ServiceProvider;

    protected TestDiFixture()
    {
        var services = new ServiceCollection();

        services.AddInfrastructureServices(options => options.UseInMemoryDatabase(DbName));
        services.AddApplicationServices();

        ServiceProvider = services.BuildServiceProvider();

        // создаём БД один раз на класс
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        ServiceProvider.Dispose();
    }
}