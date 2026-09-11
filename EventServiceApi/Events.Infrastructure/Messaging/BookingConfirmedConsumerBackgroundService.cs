using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using Events.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Events.Infrastructure.Messaging;

/// <summary>
/// Фоновый сервис-потребитель Kafka: подписывается на топик <see cref="KafkaTopics.BookingConfirmed"/>
/// и уменьшает количество свободных мест события при подтверждении брони.
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
            AutoOffsetReset = AutoOffsetReset.Earliest
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

                    HandleMessageAsync(result.Message.Value, stoppingToken).GetAwaiter().GetResult();
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
        try
        {
            var evt = JsonSerializer.Deserialize<BookingConfirmedEvent>(rawMessage);

            if (evt is null)
            {
                _logger.LogWarning("Received unparsable BookingConfirmedEvent message: {Message}", rawMessage);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
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
                return;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process BookingConfirmedEvent message: {Message}", rawMessage);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _consumer?.Close();
        _consumer?.Dispose();

        await base.StopAsync(cancellationToken);
    }
}
