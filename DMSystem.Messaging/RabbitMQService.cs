using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DMSystem.Messaging
{
    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _publishChannel;
        private readonly ILogger<RabbitMQService> _logger;
        private readonly RabbitMQSettings _settings;
        private readonly List<IModel> _consumerChannels = new();

        // Production Constructor
        public RabbitMQService(IOptions<RabbitMQSettings> options, ILogger<RabbitMQService> logger)
        {
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ValidateConfiguration();

            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                UserName = _settings.UserName,
                Password = _settings.Password,
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _logger.LogInformation("RabbitMQ connection established.");

            _publishChannel = _connection.CreateModel();

            foreach (var (_, queue) in _settings.Queues)
            {
                DeclareQueue(queue);
            }

            _connection.ConnectionShutdown += OnConnectionShutdown;
        }

        // Test Constructor
        public RabbitMQService(IConnection connection, RabbitMQSettings settings, ILogger<RabbitMQService> logger)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ValidateConfiguration();

            _publishChannel = _connection.CreateModel();

            foreach (var (_, queue) in _settings.Queues)
            {
                DeclareQueue(queue);
            }
        }

        public async Task PublishMessageAsync<T>(T message, string queueName)
        {
            if (message == null)
            {
                _logger.LogError("Cannot publish null message to {QueueName}", queueName);
                throw new ArgumentNullException(nameof(message), "Message cannot be null.");
            }

            if (!_settings.Queues.Values.Contains(queueName))
            {
                _logger.LogError("Queue {QueueName} not found in configuration.", queueName);
                throw new InvalidOperationException($"Queue {queueName} not found in configuration.");
            }

            try
            {
                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
                var properties = _publishChannel.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";

                _publishChannel.BasicPublish(
                    exchange: "",
                    routingKey: queueName,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation("Published message to {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to {QueueName}", queueName);
                throw;
            }

            await Task.CompletedTask;
        }

        public void ConsumeQueue<T>(string queueName, Func<T, Task> onMessage)
        {
            if (!_settings.Queues.Values.Contains(queueName))
            {
                _logger.LogError("Queue {QueueName} not found in configuration.", queueName);
                throw new InvalidOperationException($"Queue {queueName} not found in configuration.");
            }

            var channel = _connection.CreateModel();
            _consumerChannels.Add(channel);

            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += async (ch, ea) =>
            {
                await ProcessMessageAsync(channel, queueName, ea, onMessage);
            };

            channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("Started consuming messages from {QueueName}", queueName);
        }

        private async Task ProcessMessageAsync<T>(IModel channel, string queueName, BasicDeliverEventArgs ea, Func<T, Task> onMessage)
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var message = JsonSerializer.Deserialize<T>(json);

                if (message == null)
                {
                    _logger.LogError("Failed to deserialize message from {QueueName}.", queueName);
                    SendToDeadLetterQueue(channel, queueName, ea);
                    return;
                }

                await onMessage(message);
                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from {QueueName}.", queueName);
                SendToDeadLetterQueue(channel, queueName, ea);
            }
        }

        private void SendToDeadLetterQueue(IModel channel, string queueName, BasicDeliverEventArgs ea)
        {
            var dlqName = $"{queueName}-dlq";

            try
            {
                DeclareQueue(dlqName);

                channel.BasicPublish(
                    exchange: "",
                    routingKey: dlqName,
                    basicProperties: ea.BasicProperties,
                    body: ea.Body.ToArray());

                _logger.LogWarning("Message sent to Dead Letter Queue: {DLQName}", dlqName);
            }
            catch (Exception dlqEx)
            {
                _logger.LogError(dlqEx, "Failed to send message to Dead Letter Queue: {DLQName}", dlqName);
            }

            channel.BasicNack(ea.DeliveryTag, false, requeue: false);
        }

        private void DeclareQueue(string queueName)
        {
            try
            {
                _publishChannel.QueueDeclare(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                _logger.LogInformation("Declared queue: {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to declare queue: {QueueName}", queueName);
                throw;
            }
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_settings.HostName) ||
                string.IsNullOrWhiteSpace(_settings.UserName) ||
                string.IsNullOrWhiteSpace(_settings.Password))
            {
                throw new InvalidOperationException("RabbitMQ settings are incomplete. Ensure HostName, UserName, and Password are configured.");
            }

            if (_settings.Queues == null || !_settings.Queues.Any())
            {
                throw new InvalidOperationException("RabbitMQ queues are not configured. Ensure at least one queue is defined.");
            }
        }

        private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection shut down: {Reason}", e.ReplyText);
        }

        public void Dispose()
        {
            foreach (var channel in _consumerChannels)
            {
                channel.Close();
                channel.Dispose();
            }

            _publishChannel.Close();
            _publishChannel.Dispose();
            _connection.Close();
            _connection.Dispose();

            _logger.LogInformation("RabbitMQ resources disposed.");
        }
    }
}
