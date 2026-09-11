using Bookings.Application.Interfaces;
using Bookings.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bookings.Infrastructure.BackgroundServices;

public sealed class BookingProcessingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    public BookingProcessingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                List<Guid> pendingIds;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

                    var pending = await bookingService.GetPendingBookingsAsync(stoppingToken);
                    pendingIds = pending.Select(b => b.Id).ToList();
                }

                var tasks = pendingIds.Select(id => ProcessBookingAsync(id, stoppingToken));
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

    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken stoppingToken)
    {
        // Имитация внешнего вызова (например, платёж/проверка) — параллельно
        await Task.Delay(ProcessingDelay, stoppingToken);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

            // Берём актуальную бронь (могла уже обработаться)
            var current = await bookingService.GetBookingByIdAsync(bookingId, stoppingToken);
            if (current is null)
                return;

            if (current.Status != BookingStatus.Pending)
                return;

            // Подтверждаем бронь; после успешного сохранения BookingService сам публикует событие в Kafka.
            var processed = await bookingService.TryProcessPendingAsync(bookingId, stoppingToken);
            if (processed)
            {
                _logger.LogInformation("Booking {BookingId} confirmed.", bookingId);
                return;
            }

            // Если не получилось подтвердить (например, статус уже сменился) — просто выходим
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // штатная остановка
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing booking {BookingId}", bookingId);

            // Компенсация: пытаемся отклонить pending-бронь
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

                var rejected = await bookingService.TryRejectPendingAsync(
                    bookingId,
                    "Unexpected processing error",
                    stoppingToken);

                if (rejected)
                {
                    _logger.LogWarning(
                        "Booking {BookingId} rejected due to processing error.",
                        bookingId);
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
                    bookingId);
            }
        }
    }
}
