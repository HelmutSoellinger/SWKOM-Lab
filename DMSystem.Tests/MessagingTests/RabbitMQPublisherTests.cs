using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using Xunit;
using DMSystem.Messaging;

namespace DMSystem.Tests.MessagingTests
{
    public class RabbitMQPublisherTests
    {
        private readonly Mock<IConnection> _mockConnection;
        private readonly Mock<IModel> _mockChannel;
        private readonly Mock<ILogger<RabbitMQPublisher<string>>> _mockLogger;

        public RabbitMQPublisherTests()
        {
            _mockConnection = new Mock<IConnection>();
            _mockChannel = new Mock<IModel>();
            _mockLogger = new Mock<ILogger<RabbitMQPublisher<string>>>();

            // Ensure CreateModel returns a mock channel
            _mockConnection.Setup(c => c.CreateModel()).Returns(_mockChannel.Object);
        }

        [Fact]
        public async Task PublishMessageAsync_PublishesMessageToQueue()
        {
            // Arrange
            var publisher = new RabbitMQPublisher<string>(_mockConnection.Object, _mockLogger.Object);

            var testMessage = "Test Message";
            var queueName = RabbitMQQueues.OrderValidationQueue;

            // Mock QueueDeclare
            _mockChannel.Setup(c => c.QueueDeclare(queueName, true, false, false, null));

            // Act
            await publisher.PublishMessageAsync(testMessage);

            // Assert
            // Verify QueueDeclare is called
            _mockChannel.Verify(
                c => c.QueueDeclare(queueName, true, false, false, null),
                Times.Once,
                "QueueDeclare was not called as expected."
            );

            // Verify logging
            _mockLogger.Verify(
                log => log.LogInformation(It.Is<string>(s => s.Contains("Message published to queue"))),
                Times.Once,
                "Log for successful publishing was not written as expected."
            );
        }

        [Fact]
        public async Task PublishMessageAsync_LogsErrorOnFailure()
        {
            // Arrange
            var publisher = new RabbitMQPublisher<string>(_mockConnection.Object, _mockLogger.Object);
            var testMessage = "Test Message";

            // Simulate exception during QueueDeclare
            _mockChannel
                .Setup(c => c.QueueDeclare(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), null))
                .Throws(new RabbitMQ.Client.Exceptions.BrokerUnreachableException(null));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<RabbitMQ.Client.Exceptions.BrokerUnreachableException>(() =>
                publisher.PublishMessageAsync(testMessage)
            );

            // Verify logging for failure
            _mockLogger.Verify(
                log => log.LogError(It.Is<string>(s => s.Contains("Failed to publish message"))),
                Times.Once,
                "Log for publishing error was not written as expected."
            );
        }

        [Fact]
        public void Dispose_ClosesConnections()
        {
            // Arrange
            var publisher = new RabbitMQPublisher<string>(_mockConnection.Object, _mockLogger.Object);

            // Act
            publisher.Dispose();

            // Assert
            // Verify the channel and connection are closed
            _mockChannel.Verify(c => c.Close(), Times.Once, "Channel.Close was not called.");
            _mockConnection.Verify(c => c.Close(), Times.Once, "Connection.Close was not called.");
        }
    }
}