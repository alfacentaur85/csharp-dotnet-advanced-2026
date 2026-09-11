using Users.IntegrationTests.Fixtures;
using Users.Infrastructure.DataAccess;
using Users.Domain.Enums;
using Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Users.IntegrationTests;

[Collection(PostgresCollection.Name)]
public abstract class RepositoryTestBase : IAsyncLifetime
{
    private readonly string _connectionString;

    protected RepositoryTestBase(PostgresContainerFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected UsersDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new UsersDbContext(options);
    }

    protected static User MakeUser(
        string login = "user",
        string passwordHash = "hash",
        UserRole role = UserRole.User)
        => User.Create(login, passwordHash, role);
}
