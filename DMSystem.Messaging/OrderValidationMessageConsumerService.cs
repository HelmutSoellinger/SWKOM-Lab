using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DMSystem.Messaging
{
    public class OrderValidationMessageConsumerService : BackgroundService
    {
        private readonly RabbitMQSetting _rabbitMqSetting;
        private IConnection? _connection;
        private IModel? _channel;
        private readonly ILogger<OrderValidationMessageConsumerService> _logger;

        public OrderValidationMessageConsumerService(
            IOptions<RabbitMQSetting> rabbitMqSetting,
            ILogger<OrderValidationMessageConsumerService> logger)
        {
            _rabbitMqSetting = rabbitMqSetting.Value;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting RabbitMQ consumer service...");
            InitializeRabbitMQ();
            return Task.CompletedTask;
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

                // Declare the queue
                _channel.QueueDeclare(queue: RabbitMQQueues.OrderValidationQueue,
                                      durable: true,
                                      exclusive: false,
                                      autoDelete: false,
                                      arguments: null);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += Consumer_Received;

                _channel.BasicConsume(queue: RabbitMQQueues.OrderValidationQueue,
                                      autoAck: false,
                                      consumer: consumer);

                _logger.LogInformation("RabbitMQ connection and consumer initialized.");
            }
            catch (BrokerUnreachableException ex)
            {
                _logger.LogError($"Failed to connect to RabbitMQ: {ex.Message}");
                throw;
            }
        }

        private void Consumer_Received(object? sender, BasicDeliverEventArgs ea)
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                _logger.LogInformation($" [x] Received {message}");

                // Process the message (your business logic here)
                ProcessMessage(message);

                // Acknowledge message
                _channel?.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing message: {ex.Message}");
                // Reject the message and requeue it
                _channel?.BasicNack(ea.DeliveryTag, false, true);
            }
        }

        private void ProcessMessage(string message)
        {
            // Add your business logic here
            _logger.LogInformation($"Processing message: {message}");
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() =>
            {
                _logger.LogInformation("RabbitMQ Consumer is stopping...");
                Dispose();
            });

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            if (_channel?.IsOpen == true)
            {
                _channel.Close();
                _channel.Dispose();
            }

            if (_connection?.IsOpen == true)
            {
                _connection.Close();
                _connection.Dispose();
            }

            base.Dispose();
        }
    }
}
