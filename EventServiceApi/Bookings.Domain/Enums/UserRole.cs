namespace Bookings.Domain.Enums;

/// <summary>
/// Роль пользователя, извлекается из claims JWT-токена (сервис Bookings не хранит пользователей —
/// роль и id пользователя приходят только из проверенного токена).
/// </summary>
public enum UserRole
{
    User = 0,
    Admin = 1
}
