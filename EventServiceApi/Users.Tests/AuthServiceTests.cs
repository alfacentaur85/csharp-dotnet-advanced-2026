using FluentAssertions;
using Moq;
using Users.Application.Interfaces;
using Users.Application.Services;
using Users.Domain.Entities;
using Users.Domain.Enums;
using Users.Domain.Exceptions;

namespace Users.Tests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private AuthService CreateSut() => new(
        _userRepositoryMock.Object,
        _passwordHasherMock.Object,
        _jwtTokenServiceMock.Object,
        _unitOfWorkMock.Object);

    [Fact]
    public async Task RegisterAsync_NewLogin_CreatesUser_AndReturnsToken()
    {
        var ct = CancellationToken.None;

        _userRepositoryMock
            .Setup(r => r.GetByLoginAsync("alice", ct))
            .ReturnsAsync((User?)null);
        _passwordHasherMock
            .Setup(h => h.Hash("password123"))
            .Returns("hashed-password");
        _jwtTokenServiceMock
            .Setup(j => j.GenerateToken(It.IsAny<Guid>(), "alice", UserRole.User))
            .Returns("jwt-token");

        var sut = CreateSut();

        var result = await sut.RegisterAsync("alice", "password123", cancellationToken: ct);

        result.Token.Should().Be("jwt-token");
        result.UserId.Should().NotBe(Guid.Empty);
        _userRepositoryMock.Verify(r => r.Add(It.Is<User>(u => u.Login == "alice" && u.PasswordHash == "hashed-password")), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ExistingLogin_ThrowsLoginAlreadyExistsException()
    {
        var ct = CancellationToken.None;
        var existing = User.Create("bob", "hash");

        _userRepositoryMock
            .Setup(r => r.GetByLoginAsync("bob", ct))
            .ReturnsAsync(existing);

        var sut = CreateSut();

        var act = () => sut.RegisterAsync("bob", "otherPassword", cancellationToken: ct);

        await act.Should().ThrowAsync<LoginAlreadyExistsException>();
        _userRepositoryMock.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        var ct = CancellationToken.None;
        var user = User.Create("carol", "hashed-password");

        _userRepositoryMock
            .Setup(r => r.GetByLoginAsync("carol", ct))
            .ReturnsAsync(user);
        _passwordHasherMock
            .Setup(h => h.Verify("password123", "hashed-password"))
            .Returns(true);
        _jwtTokenServiceMock
            .Setup(j => j.GenerateToken(user.Id, "carol", UserRole.User))
            .Returns("jwt-token");

        var sut = CreateSut();

        var result = await sut.LoginAsync("carol", "password123", ct);

        result.Token.Should().Be("jwt-token");
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task LoginAsync_UnknownLogin_ThrowsInvalidCredentialsException()
    {
        var ct = CancellationToken.None;

        _userRepositoryMock
            .Setup(r => r.GetByLoginAsync("unknown", ct))
            .ReturnsAsync((User?)null);

        var sut = CreateSut();

        var act = () => sut.LoginAsync("unknown", "password123", ct);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsInvalidCredentialsException()
    {
        var ct = CancellationToken.None;
        var user = User.Create("dave", "hashed-password");

        _userRepositoryMock
            .Setup(r => r.GetByLoginAsync("dave", ct))
            .ReturnsAsync(user);
        _passwordHasherMock
            .Setup(h => h.Verify("wrongPassword", "hashed-password"))
            .Returns(false);

        var sut = CreateSut();

        var act = () => sut.LoginAsync("dave", "wrongPassword", ct);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }
}
