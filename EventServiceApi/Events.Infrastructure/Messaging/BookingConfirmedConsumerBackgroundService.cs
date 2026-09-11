using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using Events.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Events.Infrastructure.Messaging;

/// <summary>
/// Фоновый сервис-потребитель Kafka: подписывается на топик <see cref="KafkaTopics.BookingConfirmed"/>
/// и уменьшает количество свободных мест события при подтверждении брони.
///
/// Идемпотентность: обработка защищена инбокс-таблицей processed_booking_events (уникальный ключ — BookingId),
/// а офсет коммитится вручную только после того, как сообщение гарантированно обработано (успешно, отклонено
/// по бизнес-причине или распознано как дубликат). Так повторная доставка того же BookingConfirmedEvent
/// (после рестарта, ребаланса или ретрая продюсера) не может списать места дважды.
/// </summary>
public sealed class BookingConfirmedConsumerBackgroundService : BackgroundService
{
    private readonly KafkaConsumerOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingConfirmedConsumerBackgroundService> _logger;
    private IConsumer<Ignore, string>? _consumer;

    public BookingConfirmedConsumerBackgroundService(
        IOptions<KafkaConsumerOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<BookingConfirmedConsumerBackgroundService> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            GroupId = _options.ConsumerGroup,
            BootstrapServers = _options.BootstrapServers,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            // Коммитим офсет сами, только после того как сообщение гарантированно обработано
            // (см. HandleMessageAsync) — иначе авто-коммит по таймеру может продвинуть офсет
            // до того, как мы реально применили изменение, и упавший процесс "потеряет" сообщение.
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        _consumer.Subscribe(KafkaTopics.BookingConfirmed);

        try
        {
            await Task.Run(() =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    ConsumeResult<Ignore, string>? result = null;

                    try
                    {
                        result = _consumer.Consume(stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message from topic '{Topic}'.", KafkaTopics.BookingConfirmed);
                        continue;
                    }

                    if (result?.Message is null)
                        continue;

                    try
                    {
                        HandleMessageAsync(result.Message.Value, stoppingToken).GetAwaiter().GetResult();

                        // Сообщение обработано (успех, бизнес-отказ или дубликат) — можно продвинуть офсет.
                        _consumer.Commit(result);
                    }
                    catch (Exception ex)
                    {
                        // Непредвиденная ошибка (например, БД временно недоступна): офсет НЕ коммитим,
                        // чтобы после восстановления сервиса сообщение было передоставлено. Повторная
                        // обработка безопасна благодаря инбоксу processed_booking_events.
                        _logger.LogError(ex, "Failed to process BookingConfirmedEvent message, offset will not be committed: {Message}", result.Message.Value);
                    }
                }
            }, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Ожидаемо при остановке сервиса.
        }
    }

    private async Task HandleMessageAsync(string rawMessage, CancellationToken cancellationToken)
    {
        var evt = JsonSerializer.Deserialize<BookingConfirmedEvent>(rawMessage);

        if (evt is null)
        {
            _logger.LogWarning("Received unparsable BookingConfirmedEvent message: {Message}", rawMessage);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var processedBookingEventStore = scope.ServiceProvider.GetRequiredService<IProcessedBookingEventStore>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var eventEntity = await eventRepository.GetByIdTrackedAsync(evt.EventId, cancellationToken);

        if (eventEntity is null)
        {
            _logger.LogWarning("BookingConfirmedEvent references unknown EventId {EventId}. Skipping.", evt.EventId);
            return;
        }

        if (!eventEntity.TryReserveSeats(evt.SeatsCount))
        {
            _logger.LogWarning(
                "Not enough available seats for EventId {EventId} to reserve {SeatsCount} seat(s). Skipping.",
                evt.EventId,
                evt.SeatsCount);
        }

        // Маркер идемпотентности добавляется независимо от исхода резервирования: если это же
        // BookingId придёт повторно, он не должен переигрывать ни успешный, ни отклонённый исход.
        processedBookingEventStore.MarkProcessed(evt.BookingId, evt.EventId);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // BookingId уже был обработан ранее (дубликат из Kafka: повторная доставка после
            // рестарта/ребаланса или ретрай продюсера) — изменение AvailableSeats отменяется вместе
            // с остальной транзакцией, повторного списания мест не происходит.
            _logger.LogInformation(
                "BookingConfirmedEvent {BookingId} for EventId {EventId} already processed, skipping duplicate.",
                evt.BookingId,
                evt.EventId);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _consumer?.Close();
        _consumer?.Dispose();

        await base.StopAsync(cancellationToken);
    }
}
