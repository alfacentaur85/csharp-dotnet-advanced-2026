using EventService.Application.DependencyInjection;
using EventService.Application.Interfaces;
using EventService.Infrastructure.DataAccess;
using EventService.Infrastructure.DependencyInjection;
using EventService.Infrastructure.Security;
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

        services.Configure<JwtOptions>(options =>
        {
            options.Secret = "test-secret-key-for-unit-tests-0123456789";
            options.Issuer = "TestIssuer";
            options.Audience = "TestAudience";
            options.ExpiresInMinutes = 60;
        });
        services.AddScoped<IJwtTokenService, JwtTokenService>();

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