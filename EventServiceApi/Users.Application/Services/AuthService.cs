using Users.Application.Dto;
using Users.Application.Interfaces;
using Users.Domain.Entities;
using Users.Domain.Enums;
using Users.Domain.Exceptions;

namespace Users.Application.Services;

/// <summary>
/// Реализация сервиса регистрации и входа пользователей.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUnitOfWork _unitOfWork;

    private static readonly SemaphoreSlim _registerSemaphore = new(1, 1);

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthResponseDto> RegisterAsync(string login, string password, UserRole role = UserRole.User, CancellationToken cancellationToken = default)
    {
        await _registerSemaphore.WaitAsync(cancellationToken);
        try
        {
            var existing = await _userRepository.GetByLoginAsync(login, cancellationToken);

            if (existing is not null)
                throw new LoginAlreadyExistsException("Пользователь с таким логином уже существует.");

            var passwordHash = _passwordHasher.Hash(password);
            var user = User.Create(login, passwordHash, role);

            _userRepository.Add(user);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var token = _jwtTokenService.GenerateToken(user.Id, user.Login, user.Role);
            return new AuthResponseDto { UserId = user.Id, Token = token };
        }
        finally
        {
            _registerSemaphore.Release();
        }
    }

    public async Task<AuthResponseDto> LoginAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByLoginAsync(login, cancellationToken);

        if (user is null || !_passwordHasher.Verify(password, user.PasswordHash))
            throw new InvalidCredentialsException("Неверный логин или пароль.");

        var token = _jwtTokenService.GenerateToken(user.Id, user.Login, user.Role);
        return new AuthResponseDto { UserId = user.Id, Token = token };
    }
}
