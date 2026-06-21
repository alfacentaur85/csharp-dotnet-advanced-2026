using EventServiceApi.DataAccess;
using EventServiceApi.Interfaces;
using EventServiceApi.Services;
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

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(DbName));

        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();

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