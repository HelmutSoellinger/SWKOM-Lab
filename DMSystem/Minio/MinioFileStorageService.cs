using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.IO;

namespace DMSystem.Minio
{
    public class MinioFileStorageService : IMinioFileStorageService
    {
        private readonly IMinioClient _minioClient;
        private readonly string _bucketName;
        private readonly ILogger<MinioFileStorageService> _logger;

        /// <summary>
        /// Initializes the MinioFileStorageService with configuration settings and logging.
        /// </summary>
        public MinioFileStorageService(IOptions<MinioSettings> options, ILogger<MinioFileStorageService> logger)
        {
            var settings = options.Value;

            // Initialize MinIO client
            _minioClient = new MinioClient()
                .WithEndpoint(settings.Endpoint)
                .WithCredentials(settings.AccessKey, settings.SecretKey)
                .Build();

            _bucketName = settings.BucketName;
            _logger = logger;
        }

        /// <summary>
        /// Ensures the bucket exists; creates it if not found.
        /// </summary>
        public async Task InitializeBucketAsync()
        {
            _logger.LogInformation("Checking if bucket '{BucketName}' exists...", _bucketName);
            try
            {
                bool exists = await _minioClient.BucketExistsAsync(
                    new BucketExistsArgs().WithBucket(_bucketName));
                if (!exists)
                {
                    _logger.LogInformation("Bucket '{BucketName}' does not exist. Creating...", _bucketName);
                    await _minioClient.MakeBucketAsync(
                        new MakeBucketArgs().WithBucket(_bucketName));
                    _logger.LogInformation("Bucket '{BucketName}' created successfully.", _bucketName);
                }
                else
                {
                    _logger.LogInformation("Bucket '{BucketName}' already exists.", _bucketName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing bucket '{BucketName}'.", _bucketName);
                throw;
            }
        }

        /// <summary>
        /// Uploads a file to the MinIO bucket.
        /// </summary>
        public async Task UploadFileAsync(string objectName, Stream fileStream, long fileSize, string contentType)
        {
            try
            {
                await _minioClient.PutObjectAsync(new PutObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName)
                    .WithStreamData(fileStream)
                    .WithObjectSize(fileSize)
                    .WithContentType(contentType));
                _logger.LogInformation("File '{ObjectName}' uploaded successfully.", objectName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file '{ObjectName}'.", objectName);
                throw;
            }
        }

        /// <summary>
        /// Downloads a file from the MinIO bucket.
        /// </summary>
        public async Task<Stream> DownloadFileAsync(string objectName)
        {
            try
            {
                var memoryStream = new MemoryStream();
                await _minioClient.GetObjectAsync(new GetObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName)
                    .WithCallbackStream(stream =>
                    {
                        stream.CopyTo(memoryStream);
                    }));
                memoryStream.Seek(0, SeekOrigin.Begin);
                _logger.LogInformation("File '{ObjectName}' downloaded successfully.", objectName);
                return memoryStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file '{ObjectName}'.", objectName);
                throw;
            }
        }

        /// <summary>
        /// Deletes a file from the MinIO bucket.
        /// </summary>
        public async Task DeleteFileAsync(string objectName)
        {
            try
            {
                await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName));
                _logger.LogInformation("File '{ObjectName}' deleted successfully.", objectName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file '{ObjectName}'.", objectName);
                throw;
            }
        }

        /// <summary>
        /// Checks if a file exists in the MinIO bucket.
        /// </summary>
        public async Task<bool> FileExistsAsync(string objectName)
        {
            try
            {
                var args = new StatObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName);

                await _minioClient.StatObjectAsync(args);
                _logger.LogInformation("File '{ObjectName}' exists.", objectName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("File '{ObjectName}' does not exist: {Message}", objectName, ex.Message);
                return false;
            }
        }
    }
}
