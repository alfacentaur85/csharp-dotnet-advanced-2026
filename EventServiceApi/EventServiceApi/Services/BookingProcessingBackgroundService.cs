// Services/BookingProcessingBackgroundService.cs
using EventServiceApi.Enums;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;

namespace EventServiceApi.Services;

public sealed class BookingProcessingBackgroundService : BackgroundService
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    public BookingProcessingBackgroundService(
        IBookingService bookingService,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _bookingService = bookingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // период опроса
        var pollInterval = TimeSpan.FromSeconds(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pending = await _bookingService.GetPendingBookingsAsync();

                foreach (var booking in pending)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    // имитация обращения к внешней системе
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

                    // перевод в Confirmed + ProcessedAt
                    var updated = new Booking
                    {
                        Id = booking.Id,
                        EventId = booking.EventId,
                        Status = BookingStatus.Confirmed,
                        CreatedAt = booking.CreatedAt,
                        ProcessedAt = DateTime.UtcNow
                    };

                    var saved = await _bookingService.TryUpdateBookingAsync(updated);
                    if (!saved)
                        _logger.LogWarning("Booking {BookingId} not found during update", booking.Id);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // штатная остановка
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing bookings");
            }

            await Task.Delay(pollInterval, stoppingToken);
        }
    }
}