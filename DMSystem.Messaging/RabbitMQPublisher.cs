using RabbitMQ.Client;
using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;  // Or use System.Text.Json if preferred
using Microsoft.Extensions.Logging;

namespace DMSystem.Messaging
{
    public class RabbitMQPublisher<T> : IRabbitMQPublisher<T>, IDisposable
    {
        private readonly RabbitMQSetting _rabbitMqSetting;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQPublisher<T>> _logger;

        // Constructor for production use
        public RabbitMQPublisher(IOptions<RabbitMQSetting> rabbitMqSetting, ILogger<RabbitMQPublisher<T>> logger)
        {
            var factory = new ConnectionFactory
            {
                HostName = rabbitMqSetting.Value.HostName,
                UserName = rabbitMqSetting.Value.UserName,
                Password = rabbitMqSetting.Value.Password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _logger = logger;
        }

        // Constructor for testing
        public RabbitMQPublisher(IConnection connection, ILogger<RabbitMQPublisher<T>> logger)
        {
            _connection = connection;
            _channel = _connection.CreateModel();
            _logger = logger;
        }

        public Task PublishMessageAsync(T message, string queueName = RabbitMQQueues.OrderValidationQueue)
        {
            try
            {
                var messageJson = JsonConvert.SerializeObject(message);
                var body = Encoding.UTF8.GetBytes(messageJson);

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;

                _channel.BasicPublish(
                    exchange: "",
                    routingKey: queueName,
                    basicProperties: properties,
                    body: body
                );

                _logger.LogInformation($"Message published to queue {queueName}: {messageJson}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to publish message to queue {queueName}: {ex.Message}");
                throw;
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try
            {
                _channel?.Close();
                _connection?.Close();
                _logger.LogInformation("RabbitMQPublisher disposed and connections closed.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while disposing RabbitMQPublisher: {ex.Message}");
            }
        }
    }

    public interface IRabbitMQPublisher<T>
    {
        Task PublishMessageAsync(T message, string queueName = RabbitMQQueues.OrderValidationQueue);
    }
}