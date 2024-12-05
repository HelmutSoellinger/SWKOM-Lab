using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Options;
using System.IO;

namespace DMSystem.Minio
{
    public class MinioFileStorageService
    {
        private readonly IMinioClient _minioClient; // MinIO client instance
        private readonly string _bucketName; // Name of the bucket

        /// <summary>
        /// Initializes the MinioFileStorageService with configuration settings.
        /// </summary>
        /// <param name="options">MinIO settings provided via dependency injection.</param>
        public MinioFileStorageService(IOptions<MinioSettings> options)
        {
            var settings = options.Value;

            // Initialize MinIO client
            _minioClient = new MinioClient()
                .WithEndpoint(settings.Endpoint)
                .WithCredentials(settings.AccessKey, settings.SecretKey)
                .Build();

            _bucketName = settings.BucketName;
        }

        /// <summary>
        /// Ensures the bucket exists; creates it if not found.
        /// </summary>
        public async Task InitializeBucketAsync()
        {
            try
            {
                bool exists = await _minioClient.BucketExistsAsync(
                    new BucketExistsArgs().WithBucket(_bucketName));
                if (!exists)
                {
                    await _minioClient.MakeBucketAsync(
                        new MakeBucketArgs().WithBucket(_bucketName));
                    Console.WriteLine($"Bucket '{_bucketName}' created successfully.");
                }
                else
                {
                    Console.WriteLine($"Bucket '{_bucketName}' already exists.");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error initializing bucket: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Uploads a file to the MinIO bucket.
        /// </summary>
        /// <param name="objectName">Unique identifier for the file in the bucket.</param>
        /// <param name="fileStream">Stream containing the file content.</param>
        /// <param name="fileSize">Size of the file in bytes.</param>
        /// <param name="contentType">MIME type of the file.</param>
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
                Console.WriteLine($"File '{objectName}' uploaded successfully.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error uploading file '{objectName}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Downloads a file from the MinIO bucket.
        /// </summary>
        /// <param name="objectName">Unique identifier for the file in the bucket.</param>
        /// <returns>Stream containing the file content.</returns>
        public async Task<Stream> DownloadFileAsync(string objectName)
        {
            try
            {
                MemoryStream memoryStream = new MemoryStream();
                await _minioClient.GetObjectAsync(new GetObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName)
                    .WithCallbackStream(stream =>
                    {
                        stream.CopyTo(memoryStream);
                    }));
                memoryStream.Seek(0, SeekOrigin.Begin); // Reset stream position
                Console.WriteLine($"File '{objectName}' downloaded successfully.");
                return memoryStream;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error downloading file '{objectName}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Deletes a file from the MinIO bucket.
        /// </summary>
        /// <param name="objectName">Unique identifier for the file in the bucket.</param>
        public async Task DeleteFileAsync(string objectName)
        {
            try
            {
                await _minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName));
                Console.WriteLine($"File '{objectName}' deleted successfully.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error deleting file '{objectName}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Checks if a file exists in the MinIO bucket.
        /// </summary>
        /// <param name="objectName">Unique identifier for the file in the bucket.</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        public async Task<bool> FileExistsAsync(string objectName)
        {
            try
            {
                var args = new StatObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(objectName);

                await _minioClient.StatObjectAsync(args);
                return true; // File exists
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File '{objectName}' not found: {ex.Message}");
                return false; // File does not exist
            }
        }
    }
}
