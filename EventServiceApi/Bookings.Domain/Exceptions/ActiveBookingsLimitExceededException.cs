namespace Bookings.Domain.Exceptions;

public sealed class ActiveBookingsLimitExceededException : Exception
{
    public ActiveBookingsLimitExceededException()
        : base("Active bookings limit exceeded for this user.")
    {
    }
}
