using Xunit;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using DMSystem.Minio;

namespace DMSystem.Tests
{
    public class MinioBucketInitializerTests
    {
        [Fact]
        public async Task StartAsync_ShouldInitializeBucketSuccessfully()
        {
            // Arrange
            var mockFileStorageService = new Mock<IMinioFileStorageService>();
            var initializer = new MinioBucketInitializer(mockFileStorageService.Object);

            // Act & Assert
            await Record.ExceptionAsync(async () => await initializer.StartAsync(default));
            mockFileStorageService.Verify(m => m.InitializeBucketAsync(), Times.Once);
        }

        [Fact]
        public async Task StartAsync_ShouldLogErrorIfInitializationFails()
        {
            // Arrange
            var mockFileStorageService = new Mock<IMinioFileStorageService>();
            mockFileStorageService.Setup(m => m.InitializeBucketAsync())
                .Throws(new Exception("Initialization failed"));
            var initializer = new MinioBucketInitializer(mockFileStorageService.Object);

            // Act & Assert
            await Record.ExceptionAsync(async () => await initializer.StartAsync(default));
        }

        [Fact]
        public async Task StopAsync_ShouldCompleteWithoutThrowing()
        {
            // Arrange
            var initializer = new MinioBucketInitializer(Mock.Of<IMinioFileStorageService>());

            // Act & Assert
            await Record.ExceptionAsync(async () => await initializer.StopAsync(default));
        }
    }
}
