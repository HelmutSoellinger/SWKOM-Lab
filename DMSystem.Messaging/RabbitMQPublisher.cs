using RabbitMQ.Client;
using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;  // Or use System.Text.Json if preferred
using Microsoft.Extensions.Logging;

namespace DMSystem.Messaging
{
    public class RabbitMQPublisher<T> : IRabbitMQPublisher<T>
    {
        private readonly RabbitMQSetting _rabbitMqSetting;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQPublisher<T>> _logger;

        public RabbitMQPublisher(IOptions<RabbitMQSetting> rabbitMqSetting, ILogger<RabbitMQPublisher<T>> logger)
        {
            _rabbitMqSetting = rabbitMqSetting.Value;
            _logger = logger;

            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqSetting.HostName,
                UserName = _rabbitMqSetting.UserName,
                Password = _rabbitMqSetting.Password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Optional: Declare the queue only once
            _channel.QueueDeclare(queue: RabbitMQQueues.OrderValidationQueue,
                                  durable: true,
                                  exclusive: false,
                                  autoDelete: false,
                                  arguments: null);
        }

        public async Task PublishMessageAsync(T message, string queueName = RabbitMQQueues.OrderValidationQueue)
        {
            var messageJson = JsonConvert.SerializeObject(message);
            var body = Encoding.UTF8.GetBytes(messageJson);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true; // Ensures the message is durable

            try
            {
                // Directly call BasicPublish without wrapping in Task.Run
                _channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: properties, body: body);
                _logger.LogInformation($"Message published to queue {queueName}: {messageJson}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error publishing message: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}
