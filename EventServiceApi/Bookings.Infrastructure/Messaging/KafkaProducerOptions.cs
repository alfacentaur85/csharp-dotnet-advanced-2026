namespace Bookings.Infrastructure.Messaging;

/// <summary>
/// Настройки Kafka-продюсера. Bookings только публикует события (нет ConsumerGroup).
/// </summary>
public sealed class KafkaProducerOptions
{
    public string BootstrapServers { get; set; } = string.Empty;
}
