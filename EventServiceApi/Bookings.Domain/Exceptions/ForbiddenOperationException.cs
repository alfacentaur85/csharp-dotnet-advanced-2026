namespace Bookings.Domain.Exceptions;

public sealed class ForbiddenOperationException : Exception
{
    public ForbiddenOperationException(string message)
        : base(message)
    {
    }
}
