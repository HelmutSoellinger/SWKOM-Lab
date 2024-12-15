using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DMSystem.Messaging;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace DMSystem.Tests.Messaging
{
    public class RabbitMQServiceTests
    {
        private readonly Mock<IConnection> _mockConnection;
        private readonly Mock<IModelWrapper> _mockPublishChannel;
        private readonly Mock<ILogger<RabbitMQService>> _mockLogger;
        private readonly RabbitMQSettings _settings;
        private readonly RabbitMQService _service;

        public RabbitMQServiceTests()
        {
            // Mock dependencies
            _mockConnection = new Mock<IConnection>();
            _mockPublishChannel = new Mock<IModelWrapper>();
            _mockLogger = new Mock<ILogger<RabbitMQService>>();

            // Test RabbitMQ settings
            _settings = new RabbitMQSettings
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest",
                Queues = new Dictionary<string, string>
                {
                    { "TestQueue", "TestQueue" }
                }
            };

            // Mock the RabbitMQ connection to return the mocked publish channel
            //_mockConnection.Setup(c => c.CreateModel()).Returns(_mockPublishChannel.Object);

            // Initialize the service with mocked dependencies
            _service = new RabbitMQService(_mockConnection.Object, _settings, _mockLogger.Object);
        }

        [Fact]
        public async Task PublishMessageAsync_ValidMessage_CreatesBasicProperties()
        {
            // Arrange
            var message = new { Name = "Test Message" };
            var queueName = "TestQueue";

            _mockPublishChannel
                .Setup(c => c.CreateBasicProperties())
                .Returns(Mock.Of<IBasicProperties>());

            // Act
            await _service.PublishMessageAsync(message, queueName);

            // Assert
            _mockPublishChannel.Verify(c => c.CreateBasicProperties(), Times.Once);
        }

        [Fact]
        public async Task PublishMessageAsync_ValidMessage_PublishesToQueue()
        {
            // Arrange
            var message = new { Name = "Test Message" };
            var queueName = "TestQueue";

            var capturedExchange = string.Empty;
            var capturedRoutingKey = string.Empty;
            ReadOnlyMemory<byte> capturedBody = default;

            _mockPublishChannel
                .Setup(c => c.CreateBasicProperties())
                .Returns(Mock.Of<IBasicProperties>());

            _mockPublishChannel
                .Setup(c => c.BasicPublish(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<IBasicProperties>(),
                    It.IsAny<ReadOnlyMemory<byte>>()))
                .Callback<string, string, IBasicProperties, ReadOnlyMemory<byte>>(
                    (exchange, routingKey, properties, body) =>
                    {
                        capturedExchange = exchange;
                        capturedRoutingKey = routingKey;
                        capturedBody = body;
                    });

            // Act
            await _service.PublishMessageAsync(message, queueName);

            // Assert
            Assert.Equal("", capturedExchange); // Default exchange
            Assert.Equal(queueName, capturedRoutingKey);
            Assert.Equal(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message)), capturedBody.ToArray());
        }

        [Fact]
        public async Task PublishMessageAsync_InvalidQueue_ThrowsException()
        {
            // Arrange
            var message = new { Name = "Test Message" };
            var invalidQueueName = "InvalidQueue";

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.PublishMessageAsync(message, invalidQueueName));
        }

        [Fact]
        public async Task ConsumeQueue_ValidMessage_ProcessesMessage()
        {
            // Arrange
            var queueName = "TestQueue";
            var message = new { Name = "Test Message" };
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
            ulong deliveryTag = 1;

            _mockPublishChannel.Setup(c => c.BasicAck(deliveryTag, false)).Verifiable();

            var tcs = new TaskCompletionSource<bool>();
            Func<dynamic, Task> onMessage = msg =>
            {
                tcs.SetResult(true); // Signal that the message was processed
                return Task.CompletedTask;
            };

            _service.ConsumeQueue(queueName, onMessage);

            var deliverArgs = new BasicDeliverEventArgs
            {
                DeliveryTag = deliveryTag,
                Body = body
            };

            // Act
            await _service.HandleBasicDeliverAsync("consumerTag", deliverArgs);

            // Wait for message processing to complete
            await tcs.Task;

            // Assert
            _mockPublishChannel.Verify(c => c.BasicAck(deliveryTag, false), Times.Once);
        }

        [Fact]
        public async Task ConsumeQueue_InvalidMessage_NacksMessage()
        {
            // Arrange
            var queueName = "TestQueue";
            var invalidMessage = "Invalid JSON";
            var body = Encoding.UTF8.GetBytes(invalidMessage);
            ulong deliveryTag = 1;

            _mockPublishChannel.Setup(c => c.BasicNack(deliveryTag, false, false)).Verifiable();

            var tcs = new TaskCompletionSource<bool>();
            Func<dynamic, Task> onMessage = msg =>
            {
                tcs.SetException(new InvalidOperationException("This should not be called for invalid messages."));
                return Task.CompletedTask;
            };

            _service.ConsumeQueue<dynamic>(queueName, onMessage);

            var deliverArgs = new BasicDeliverEventArgs
            {
                DeliveryTag = deliveryTag,
                Body = body
            };

            // Act
            await _service.HandleBasicDeliverAsync("consumerTag", deliverArgs);

            // Wait to ensure the message handling completed
            await Task.Delay(100); // Allow time for potential processing

            // Assert
            _mockPublishChannel.Verify(c => c.BasicNack(deliveryTag, false, false), Times.Once);
        }
    }
}
