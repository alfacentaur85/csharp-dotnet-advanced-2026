using Users.IntegrationTests.Fixtures;
using Users.Infrastructure.DataAccess;
using Users.Infrastructure.DataAccess.Repositories;

namespace Users.IntegrationTests.Repositories;

public sealed class UserRepositoryTests : RepositoryTestBase
{
    public UserRepositoryTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GetByLoginAsync_ExistingLogin_ReturnsUntrackedUser()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);

        var user = MakeUser("alice");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await repository.GetByLoginAsync("alice");

        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.Id);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetByLoginAsync_MissingLogin_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);

        var result = await repository.GetByLoginAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsUntrackedUser()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);

        var user = MakeUser("bob");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await repository.GetByIdAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.Id);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetByIdAsync_MissingId_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task Add_ThenSaveChanges_PersistsUserVisibleFromNewContext()
    {
        await using var writeContext = CreateContext();
        var repository = new UserRepository(writeContext);
        var unitOfWork = new UnitOfWork(writeContext);

        var user = MakeUser("carol");
        repository.Add(user);
        await unitOfWork.SaveChangesAsync();

        await using var readContext = CreateContext();
        var persisted = await readContext.Users.FindAsync(user.Id);

        Assert.NotNull(persisted);
        Assert.Equal("carol", persisted!.Login);
    }

    [Fact]
    public async Task Add_DuplicateLogin_ThrowsOnSaveChanges()
    {
        await using var context = CreateContext();
        var repository = new UserRepository(context);
        var unitOfWork = new UnitOfWork(context);

        repository.Add(MakeUser("dave"));
        await unitOfWork.SaveChangesAsync();

        await using var context2 = CreateContext();
        var repository2 = new UserRepository(context2);
        var unitOfWork2 = new UnitOfWork(context2);
        repository2.Add(MakeUser("dave"));

        await Assert.ThrowsAnyAsync<Exception>(() => unitOfWork2.SaveChangesAsync());
    }
}
