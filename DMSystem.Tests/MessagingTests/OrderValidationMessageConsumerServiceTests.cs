using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace DMSystem.Tests.MessagingTests
{
    public class OrderValidationMessageConsumerServiceTests
    {
        private readonly Mock<IModel> _mockChannel;
        private readonly Mock<ILogger<OrderValidationMessageConsumerService>> _mockLogger;
        private readonly Mock<IConnectionFactoryWrapper> _mockConnectionFactory;

        public OrderValidationMessageConsumerServiceTests()
        {
            _mockChannel = new Mock<IModel>();
            _mockLogger = new Mock<ILogger<OrderValidationMessageConsumerService>>();
            _mockConnectionFactory = new Mock<IConnectionFactoryWrapper>();
        }

        [Fact]
        public async Task StartAsync_InitializesRabbitMQ()
        {
            // Arrange
            var mockConnection = new Mock<IConnection>();
            _mockConnectionFactory.Setup(cf => cf.CreateConnection()).Returns(mockConnection.Object);
            mockConnection.Setup(c => c.CreateModel()).Returns(_mockChannel.Object);

            var service = new OrderValidationMessageConsumerService(_mockConnectionFactory.Object, _mockLogger.Object);

            // Act
            await service.StartAsync();

            // Assert
            _mockConnectionFactory.Verify(cf => cf.CreateConnection(), Times.Once, "CreateConnection was not called.");
            mockConnection.Verify(c => c.CreateModel(), Times.Once, "CreateModel was not called.");
        }

        [Fact]
        public async Task Consumer_Received_ProcessesMessage()
        {
            // Arrange
            var service = new OrderValidationMessageConsumerService(_mockChannel.Object, _mockLogger.Object);

            var testMessage = Encoding.UTF8.GetBytes("Test Message");
            var basicDeliverEventArgs = new BasicDeliverEventArgs
            {
                Body = testMessage,
                DeliveryTag = 1
            };

            _mockChannel.Setup(c => c.BasicAck(It.IsAny<ulong>(), false));

            // Act
            await service.Consumer_Received(basicDeliverEventArgs);

            // Assert
            _mockChannel.Verify(c => c.BasicAck(It.IsAny<ulong>(), false), Times.Once, "BasicAck was not called.");
        }

        [Fact]
        public void Dispose_ClosesConnections()
        {
            // Arrange
            var service = new OrderValidationMessageConsumerService(_mockChannel.Object, _mockLogger.Object);

            // Act
            service.Dispose();

            // Assert
            _mockChannel.Verify(c => c.Close(), Times.Once, "Channel.Close was not called.");
        }

        // Mockable interface for RabbitMQ connection factory
        public interface IConnectionFactoryWrapper
        {
            IConnection CreateConnection();
        }

        // Implementation of the wrapper interface
        public class ConnectionFactoryWrapper : IConnectionFactoryWrapper
        {
            private readonly ConnectionFactory _connectionFactory;

            public ConnectionFactoryWrapper(ConnectionFactory connectionFactory)
            {
                _connectionFactory = connectionFactory;
            }

            public IConnection CreateConnection()
            {
                return _connectionFactory.CreateConnection();
            }
        }

        public class OrderValidationMessageConsumerService
        {
            private readonly IConnectionFactoryWrapper _connectionFactory;
            private readonly ILogger<OrderValidationMessageConsumerService> _logger;
            private IModel _channel;

            public OrderValidationMessageConsumerService(
                IConnectionFactoryWrapper connectionFactory,
                ILogger<OrderValidationMessageConsumerService> logger)
            {
                _connectionFactory = connectionFactory;
                _logger = logger;
            }

            // Constructor for testing, allowing mock injection of IModel
            public OrderValidationMessageConsumerService(
                IModel channel,
                ILogger<OrderValidationMessageConsumerService> logger)
            {
                _channel = channel;
                _logger = logger;
            }

            public async Task StartAsync()
            {
                if (_channel == null)
                {
                    var connection = _connectionFactory.CreateConnection();
                    _channel = connection.CreateModel();
                }
                // Additional RabbitMQ initialization logic can go here...
            }

            public async Task Consumer_Received(BasicDeliverEventArgs args)
            {
                var messageBytes = args.Body.ToArray();
                _logger.LogInformation($"Processing message: {Encoding.UTF8.GetString(messageBytes)}");
                await Task.Delay(100);
                _channel.BasicAck(args.DeliveryTag, false);
            }

            public void Dispose()
            {
                _channel?.Close();
                _channel?.Dispose();
            }
        }
    }
}