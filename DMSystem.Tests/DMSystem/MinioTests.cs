using Xunit;
using Moq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Minio;
using Minio.DataModel.Args;
using System.IO;
using Microsoft.Extensions.Options;
using Minio.DataModel;
using DMSystem.Minio;

namespace DMSystem.Tests.DMSystem
{
    public interface IMinioClientWrapper
    {
        Task<bool> BucketExistsAsync(string bucketName);
        Task MakeBucketAsync(string bucketName);
        Task PutObjectAsync(string bucketName, string objectName, Stream stream, long size, string contentType);
        Task<Stream> GetObjectAsync(string bucketName, string objectName);
        Task RemoveObjectAsync(string bucketName, string objectName);
        Task<ObjectStat> StatObjectAsync(string bucketName, string objectName);
    }

    public class MinioTests
    {
        private readonly Mock<IMinioClientWrapper> _minioClientMock;
        private readonly Mock<IOptions<MinioSettings>> _optionsMock;
        private readonly MinioSettings _settings;

        public MinioTests()
        {
            _minioClientMock = new Mock<IMinioClientWrapper>();
            _optionsMock = new Mock<IOptions<MinioSettings>>();
            _settings = new MinioSettings
            {
                Endpoint = "test-endpoint",
                AccessKey = "test-access-key",
                SecretKey = "test-secret-key",
                BucketName = "test-bucket"
            };
            _optionsMock.Setup(o => o.Value).Returns(_settings);
        }

        [Fact]
        public async Task MinioFileStorageService_InitializeBucketAsync_ShouldCreateBucketIfNotExists()
        {
            // Arrange
            _minioClientMock.Setup(c => c.BucketExistsAsync(_settings.BucketName))
                           .ReturnsAsync(false);
            _minioClientMock.Setup(c => c.MakeBucketAsync(_settings.BucketName));

            var service = new MinioFileStorageService(_optionsMock.Object);

            // Act
            await service.InitializeBucketAsync();

            // Assert
            _minioClientMock.Verify(c => c.MakeBucketAsync(_settings.BucketName), Times.Once);
        }

        [Fact]
        public async Task MinioFileStorageService_UploadFileAsync_ShouldUploadFile()
        {
            // Arrange
            var stream = new MemoryStream();
            _minioClientMock.Setup(c => c.PutObjectAsync(
                _settings.BucketName,
                "test-object",
                It.IsAny<Stream>(),
                It.IsAny<long>(),
                "text/plain")
            ).Callback<PutObjectArgs>(args =>
            {
                stream.Position = 0;
            });

            var service = new MinioFileStorageService(_optionsMock.Object);

            // Act
            await service.UploadFileAsync("test-object", stream, 0, "text/plain");

            // Assert
            _minioClientMock.Verify(c => c.PutObjectAsync(
                _settings.BucketName,
                "test-object",
                It.IsAny<Stream>(),
                It.IsAny<long>(),
                "text/plain"),
                Times.Once);
        }

        [Fact]
        public async Task MinioFileStorageService_DownloadFileAsync_ShouldDownloadFile()
        {
            // Arrange
            var memoryStream = new MemoryStream();
            _minioClientMock.Setup(c => c.GetObjectAsync(_settings.BucketName, "test-object"))
                          .ReturnsAsync(memoryStream);

            var service = new MinioFileStorageService(_optionsMock.Object);

            // Act
            var downloadedStream = await service.DownloadFileAsync("test-object");

            // Assert
            Assert.NotNull(downloadedStream);
            _minioClientMock.Verify(c => c.GetObjectAsync(_settings.BucketName, "test-object"), Times.Once);
        }

        [Fact]
        public async Task MinioFileStorageService_DeleteFileAsync_ShouldDeleteFile()
        {
            // Arrange
            var service = new MinioFileStorageService(_optionsMock.Object);

            // Act
            await service.DeleteFileAsync("test-object");

            // Assert
            _minioClientMock.Verify(c => c.RemoveObjectAsync(_settings.BucketName, "test-object"), Times.Once);
        }

        [Fact]
        public async Task MinioFileStorageService_FileExistsAsync_ShouldCheckFileExistence()
        {
            // Arrange
            var objectStat = new ObjectStat { Size = 100 };
            _minioClientMock.Setup(c => c.StatObjectAsync(_settings.BucketName, "test-object"))
                          .ReturnsAsync(objectStat);

            var service = new MinioFileStorageService(_optionsMock.Object);

            // Act
            bool exists = await service.FileExistsAsync("test-object");

            // Assert
            Assert.True(exists);
            _minioClientMock.Verify(c => c.StatObjectAsync(_settings.BucketName, "test-object"), Times.Once);
        }
    }

    public class ObjectStat
    {
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }
}
