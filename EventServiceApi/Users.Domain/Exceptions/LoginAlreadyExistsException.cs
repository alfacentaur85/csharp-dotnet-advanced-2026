namespace Users.Domain.Exceptions;

public sealed class LoginAlreadyExistsException : Exception
{
    public LoginAlreadyExistsException(string message)
        : base(message)
    {
    }
}
