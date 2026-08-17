namespace EventService.Domain.Exceptions;

public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException(string message)
        : base(message)
    {
    }
}
