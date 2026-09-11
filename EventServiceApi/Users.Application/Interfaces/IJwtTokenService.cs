using Users.Domain.Enums;

namespace Users.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(Guid userId, string login, UserRole role);
}
