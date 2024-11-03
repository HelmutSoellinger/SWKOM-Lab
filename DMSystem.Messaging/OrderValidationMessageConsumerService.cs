using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client.Exceptions;

namespace DMSystem.Messaging
{
    public class OrderValidationMessageConsumerService : BackgroundService
    {
        private readonly RabbitMQSetting _rabbitMqSetting;
        private IConnection _connection;
        private IModel _channel;
        private readonly ILogger<OrderValidationMessageConsumerService> _logger;

        public OrderValidationMessageConsumerService(IOptions<RabbitMQSetting> rabbitMqSetting, ILogger<OrderValidationMessageConsumerService> logger)
        {
            _rabbitMqSetting = rabbitMqSetting.Value;
            _logger = logger;

            InitializeRabbitMQ();
        }

        private void InitializeRabbitMQ()
        {
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqSetting.HostName,
                UserName = _rabbitMqSetting.UserName,
                Password = _rabbitMqSetting.Password
            };

            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declare queue
                _channel.QueueDeclare(queue: RabbitMQQueues.OrderValidationQueue,
                                      durable: true,
                                      exclusive: false,
                                      autoDelete: false,
                                      arguments: null);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += Consumer_Received;
                _channel.BasicConsume(queue: RabbitMQQueues.OrderValidationQueue, autoAck: false, consumer: consumer);
            }
            catch (BrokerUnreachableException ex)
            {
                _logger.LogError($"RabbitMQ connection failed: {ex.Message}");
                // Consider implementing retry logic here
            }
        }

        private void Consumer_Received(object sender, BasicDeliverEventArgs ea)
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                // Process the message here
                _logger.LogInformation($" [x] Received {message}");

                // Acknowledge the message
                _channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError($" [!] Error processing message: {ex.Message}");
                // Optionally handle requeueing or other strategies
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask; // The work is done in the consumer
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}
