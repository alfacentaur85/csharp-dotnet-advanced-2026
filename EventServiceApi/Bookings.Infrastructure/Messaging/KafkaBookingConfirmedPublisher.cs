using System.Text.Json;
using Bookings.Application.Interfaces;
using Confluent.Kafka;
using Contracts;
using Microsoft.Extensions.Options;

namespace Bookings.Infrastructure.Messaging;

/// <summary>
/// Kafka-реализация публикации события подтверждения брони.
/// Продюсер создаётся один раз (класс регистрируется как Singleton) и сбрасывается/освобождается при остановке приложения.
/// </summary>
public sealed class KafkaBookingConfirmedPublisher : IBookingConfirmedPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaBookingConfirmedPublisher(IOptions<KafkaProducerOptions> options)
    {
        var config = new ProducerConfig { BootstrapServers = options.Value.BootstrapServers };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(BookingConfirmedEvent bookingConfirmedEvent, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(bookingConfirmedEvent);

        await _producer.ProduceAsync(
            KafkaTopics.BookingConfirmed,
            new Message<string, string>
            {
                // Ключ — EventId, чтобы события по одному мероприятию попадали в один партишн (сохраняли порядок).
                Key = bookingConfirmedEvent.EventId.ToString(),
                Value = json
            },
            cancellationToken);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}
