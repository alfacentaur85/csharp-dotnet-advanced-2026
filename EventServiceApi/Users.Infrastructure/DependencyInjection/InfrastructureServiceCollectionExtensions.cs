using Users.Application.Interfaces;
using Users.Infrastructure.DataAccess;
using Users.Infrastructure.DataAccess.Repositories;
using Users.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Users.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        string connectionString,
        IConfiguration configuration)
    {
        services.AddDbContext<UsersDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSecurityServices(configuration);

        return services.AddInfrastructureCore();
    }

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        services.AddDbContext<UsersDbContext>(configureDbContext);

        return services.AddInfrastructureCore();
    }

    private static IServiceCollection AddInfrastructureCore(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPasswordHasher, Sha256PasswordHasher>();

        return services;
    }

    private static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(options =>
        {
            var section = configuration.GetSection("Jwt");
            options.Secret = section["Secret"] ?? string.Empty;
            options.Issuer = section["Issuer"] ?? string.Empty;
            options.Audience = section["Audience"] ?? string.Empty;
            options.ExpiresInMinutes = int.Parse(section["ExpiresInMinutes"] ?? "0");
        });
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
