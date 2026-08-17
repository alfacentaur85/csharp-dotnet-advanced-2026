using EventService.Domain.Exceptions;
using EventService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EventServiceApi.Tests;

public class AuthServiceTests : TestDiFixture
{
    [Fact]
    public async Task Register_NewLogin_CreatesUser_AndReturnsToken()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var result = await authService.RegisterAsync("alice", "password123", cancellationToken: ct);

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    [Fact]
    public async Task Register_ExistingLogin_ThrowsLoginAlreadyExistsException()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        await authService.RegisterAsync("bob", "password123", cancellationToken: ct);

        await Assert.ThrowsAsync<LoginAlreadyExistsException>(() =>
            authService.RegisterAsync("bob", "otherPassword", cancellationToken: ct));
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var ct = CancellationToken.None;

        using (var scope = ServiceProvider.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            await authService.RegisterAsync("carol", "password123", cancellationToken: ct);
        }

        using (var scope = ServiceProvider.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var result = await authService.LoginAsync("carol", "password123", ct);

            Assert.False(string.IsNullOrWhiteSpace(result.Token));
        }
    }

    [Fact]
    public async Task Login_UnknownLogin_ThrowsInvalidCredentialsException()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            authService.LoginAsync("unknown", "password123", ct));
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsInvalidCredentialsException()
    {
        var ct = CancellationToken.None;

        using (var scope = ServiceProvider.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            await authService.RegisterAsync("dave", "password123", cancellationToken: ct);
        }

        using (var scope = ServiceProvider.CreateScope())
        {
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

            await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
                authService.LoginAsync("dave", "wrongPassword", ct));
        }
    }
}
