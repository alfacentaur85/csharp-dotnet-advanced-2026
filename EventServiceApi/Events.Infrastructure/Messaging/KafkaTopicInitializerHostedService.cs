using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Events.Infrastructure.Messaging;

/// <summary>
/// Создаёт (при отсутствии) Kafka-топик <see cref="KafkaTopics.BookingConfirmed"/> при старте приложения.
/// Никогда не блокирует запуск сервиса — все ошибки логируются и подавляются.
/// </summary>
public sealed class KafkaTopicInitializerHostedService : IHostedService
{
    private readonly KafkaConsumerOptions _options;
    private readonly ILogger<KafkaTopicInitializerHostedService> _logger;

    public KafkaTopicInitializerHostedService(
        IOptions<KafkaConsumerOptions> options,
        ILogger<KafkaTopicInitializerHostedService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var adminClient = new AdminClientBuilder(new AdminClientConfig
            {
                BootstrapServers = _options.BootstrapServers
            }).Build();

            await adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification
                {
                    Name = KafkaTopics.BookingConfirmed,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                }
            });

            _logger.LogInformation("Kafka topic '{Topic}' created.", KafkaTopics.BookingConfirmed);
        }
        catch (CreateTopicsException ex) when (ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            _logger.LogInformation("Kafka topic '{Topic}' already exists.", KafkaTopics.BookingConfirmed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Kafka topic '{Topic}'. Startup continues regardless.", KafkaTopics.BookingConfirmed);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
