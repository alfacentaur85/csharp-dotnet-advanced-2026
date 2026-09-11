using Users.Application.Dto;
using Users.Domain.Entities;

namespace Users.Application.Mappings;

public static class UserMappings
{
    public static UserResponseDto ToResponseDto(this User user) => new()
    {
        Id = user.Id,
        Login = user.Login,
        Role = user.Role.ToString()
    };
}
