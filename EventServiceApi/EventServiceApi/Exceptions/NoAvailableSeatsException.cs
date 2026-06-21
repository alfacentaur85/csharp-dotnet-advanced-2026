namespace EventServiceApi.Exceptions;

public sealed class NoAvailableSeatsException : Exception
{
    public NoAvailableSeatsException()
        : base("No available seats for this event")
    {
    }
}