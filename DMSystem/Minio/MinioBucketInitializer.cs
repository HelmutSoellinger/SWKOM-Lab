using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace DMSystem.Minio
{
    public class MinioBucketInitializer : IHostedService
    {
        private readonly MinioFileStorageService _fileStorageService;

        public MinioBucketInitializer(MinioFileStorageService minioFileStorageService)
        {
            _fileStorageService = minioFileStorageService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _fileStorageService.InitializeBucketAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error initializing MinIO bucket: {ex.Message}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
