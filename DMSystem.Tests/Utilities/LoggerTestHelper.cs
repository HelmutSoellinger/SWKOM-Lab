using Microsoft.Extensions.Logging;
using Moq;

namespace DMSystem.Tests.Utilities
{
    public static class LoggerMockExtensions
    {
        public static void VerifyLog<T>(
            this Mock<ILogger<T>> loggerMock,
            LogLevel expectedLogLevel,
            string expectedMessagePart,
            Times times)
            where T : class
        {
            loggerMock.Verify(
                l => l.Log(
                    expectedLogLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, t) => state.ToString()!.Contains(expectedMessagePart)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                times);
        }
    }
}
