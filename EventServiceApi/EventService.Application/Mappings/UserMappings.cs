using EventService.Application.Dto;
using EventService.Domain.Entities;

namespace EventService.Application.Mappings;

public static class UserMappings
{
    public static UserResponseDto ToResponseDto(this User user) => new()
    {
        Id = user.Id,
        Login = user.Login,
        Role = user.Role.ToString()
    };
}
