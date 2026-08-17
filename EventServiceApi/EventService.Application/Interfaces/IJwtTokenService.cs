using EventService.Domain.Enums;

namespace EventService.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(Guid userId, string login, UserRole role);
}
