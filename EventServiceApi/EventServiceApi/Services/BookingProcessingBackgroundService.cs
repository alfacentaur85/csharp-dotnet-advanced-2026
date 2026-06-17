// Services/BookingProcessingBackgroundService.cs
using EventServiceApi.Interfaces;

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

                    // атомарно обработать pending-бронь (без дублирования логики)
                    var processed = await _bookingService.TryProcessPendingAsync(booking.Id);

                    // processed=false означает:
                    // - бронь уже обработали параллельно
                    // - бронь удалили
                    // - бронь уже не Pending
                    if (!processed)
                        _logger.LogDebug("Booking {BookingId} was not processed (already processed/removed)", booking.Id);
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