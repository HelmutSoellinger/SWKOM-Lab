using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Linq;

public class RabbitMQService : IRabbitMQService, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _publishChannel;
    private readonly ILogger<RabbitMQService> _logger;
    private readonly RabbitMQSettings _settings;

    private readonly List<IModel> _consumerChannels = new();

    public RabbitMQService(IOptions<RabbitMQSettings> options, ILogger<RabbitMQService> logger)
    {
        _settings = options.Value;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            UserName = _settings.UserName,
            Password = _settings.Password,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _logger.LogInformation("RabbitMQ connection established");

        // Create a dedicated channel for publishing
        _publishChannel = _connection.CreateModel();

        // Declare all necessary queues
        foreach (var (_, queue) in _settings.Queues)
        {
            _publishChannel.QueueDeclare(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );
            _logger.LogInformation("Declared queue: {QueueName}", queue);
        }
    }

    public Task PublishMessageAsync<T>(T message, string queueName)
    {
        if (!_settings.Queues.Values.Contains(queueName))
        {
            _logger.LogWarning("Queue {QueueName} not found in configuration.", queueName);
        }

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        var properties = _publishChannel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";

        _publishChannel.BasicPublish(exchange: "",
                                     routingKey: queueName,
                                     basicProperties: properties,
                                     body: body);

        _logger.LogInformation("Published message to {QueueName}", queueName);
        return Task.CompletedTask;
    }

    public void ConsumeQueue<T>(string queueName, Func<T, Task> onMessage)
    {
        if (!_settings.Queues.Values.Contains(queueName))
        {
            _logger.LogWarning("Queue {QueueName} not found in configuration.", queueName);
        }

        var channel = _connection.CreateModel();
        _consumerChannels.Add(channel);

        channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (ch, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var message = JsonSerializer.Deserialize<T>(json);
                if (message == null)
                {
                    _logger.LogError("Failed to deserialize message from {QueueName}", queueName);
                    channel.BasicNack(ea.DeliveryTag, false, false);
                    return;
                }

                await onMessage(message);
                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from {QueueName}", queueName);
                // Consider a DLQ strategy if needed
                channel.BasicNack(ea.DeliveryTag, false, requeue: false);
            }
        };

        channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        _logger.LogInformation("Consuming messages from {QueueName}", queueName);
    }

    public void Dispose()
    {
        foreach (var chan in _consumerChannels)
        {
            chan.Close();
            chan.Dispose();
        }

        _publishChannel?.Close();
        _publishChannel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}
