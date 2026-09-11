namespace Events.Infrastructure.Messaging;

/// <summary>
/// Опции подключения к Kafka для сервиса событий (потребитель).
/// </summary>
public sealed class KafkaConsumerOptions
{
    public string BootstrapServers { get; set; } = string.Empty;

    public string ConsumerGroup { get; set; } = string.Empty;
}
