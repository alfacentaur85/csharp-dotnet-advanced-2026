namespace EventService.Domain.Exceptions;

public sealed class PastEventBookingException : Exception
{
    public PastEventBookingException()
        : base("Cannot book a seat for an event that has already started or ended.")
    {
    }
}
