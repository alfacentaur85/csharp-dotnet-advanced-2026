using EventServiceApi.Enums;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;

namespace EventServiceApi.Services;

public sealed class BookingProcessingBackgroundService : BackgroundService
{
    private readonly IBookingService _bookingService;
    private readonly IEventService _eventService;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    // асинхронная защита критической секции записи/обновления
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    public BookingProcessingBackgroundService(
        IBookingService bookingService,
        IEventService eventService,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _bookingService = bookingService;
        _eventService = eventService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pendingBookings = (await _bookingService.GetPendingBookingsAsync()).ToList();

                // параллельный запуск обработки
                var tasks = pendingBookings.Select(b => ProcessBookingAsync(b, stoppingToken));
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // штатная остановка
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing bookings batch");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBookingAsync(Booking booking, CancellationToken stoppingToken)
    {
        // Имитация внешнего вызова — параллельно для всех броней
        await Task.Delay(ProcessingDelay, stoppingToken);

        try
        {
            // Критическая секция: проверка события + подтверждение
            await _processingSemaphore.WaitAsync(stoppingToken);
            try
            {
                // Берём актуальную бронь из хранилища (snapshot мог устареть)
                var current = await _bookingService.GetBookingByIdAsync(booking.Id);
                if (current is null)
                    return;

                // Уже обработана кем-то — выходим
                if (current.Status != BookingStatus.Pending)
                    return;

                // Если событие удалено — отклоняем (TryRejectPendingAsync сам вернёт место, если событие существует)
                if (_eventService.GetById(current.EventId) is null)
                {
                    var rejected = await _bookingService.TryRejectPendingAsync(current.Id, "Event deleted");
                    if (rejected)
                    {
                        _logger.LogWarning(
                            "Booking {BookingId} rejected because event {EventId} not found (deleted).",
                            current.Id, current.EventId);
                    }

                    return;
                }

                // Событие есть — подтверждаем атомарно (только если всё ещё Pending)
                var processed = await _bookingService.TryProcessPendingAsync(current.Id);
                if (!processed)
                    return;

                _logger.LogInformation("Booking {BookingId} confirmed.", current.Id);
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // штатная остановка — ничего не меняем
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing booking {BookingId}", booking.Id);

            // отклоняем pending-бронь и возвращаем место (если событие существует)
            try
            {
                // семафор, чтобы сериализовать обработку в background service.
                await _processingSemaphore.WaitAsync(stoppingToken);
                try
                {
                    var rejected = await _bookingService.TryRejectPendingAsync(booking.Id, "Unexpected processing error");
                    if (rejected)
                    {
                        _logger.LogWarning(
                            "Booking {BookingId} rejected due to processing error (seat released if possible).",
                            booking.Id);
                    }
                }
                finally
                {
                    _processingSemaphore.Release();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // при остановке приложения компенсацию можно не успеть сделать — допустимо
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(
                    rollbackEx,
                    "Failed to reject/rollback booking {BookingId} after processing error",
                    booking.Id);
            }
        }
    }
}