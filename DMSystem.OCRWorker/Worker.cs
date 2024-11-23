using DMSystem.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DMSystem.OCRWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly RabbitMQSetting _rabbitMQSetting;
        private IConnection _connection;
        private IModel _channel;

        public Worker(ILogger<Worker> logger, IOptions<RabbitMQSetting> rabbitMQSetting)
        {
            _logger = logger;
            _rabbitMQSetting = rabbitMQSetting.Value; // Load RabbitMQ settings from DI
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting RabbitMQ worker...");
            InitializeRabbitMQ();
            return Task.CompletedTask;
        }

        private void InitializeRabbitMQ()
        {
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMQSetting.HostName,
                UserName = _rabbitMQSetting.UserName,
                Password = _rabbitMQSetting.Password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(
                queue: _rabbitMQSetting.QueueName, // Use the QueueName from settings
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation($"Received message: {message}");

                // TODO: Add OCR processing logic here
            };

            _channel.BasicConsume(
                queue: _rabbitMQSetting.QueueName,
                autoAck: true,
                consumer: consumer
            );

            _logger.LogInformation("RabbitMQ initialized successfully.");
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() =>
            {
                _logger.LogInformation("Worker is stopping...");
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
