using FluentAssertions;
using Moq;
using Users.Application.Interfaces;
using Users.Application.Services;
using Users.Domain.Entities;

namespace Users.Tests;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();

    private UserService CreateSut() => new(_userRepositoryMock.Object);

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsUser()
    {
        var ct = CancellationToken.None;
        var user = User.Create("erin", "hash");

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, ct))
            .ReturnsAsync(user);

        var sut = CreateSut();

        var result = await sut.GetByIdAsync(user.Id, ct);

        result.Should().BeSameAs(user);
    }

    [Fact]
    public async Task GetByIdAsync_MissingId_ReturnsNull()
    {
        var ct = CancellationToken.None;
        var id = Guid.NewGuid();

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(id, ct))
            .ReturnsAsync((User?)null);

        var sut = CreateSut();

        var result = await sut.GetByIdAsync(id, ct);

        result.Should().BeNull();
    }
}
