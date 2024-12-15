using System.IO;
using System.Threading.Tasks;

namespace DMSystem.Minio
{
    public interface IMinioFileStorageService
    {
        Task InitializeBucketAsync();
        Task UploadFileAsync(string objectName, Stream fileStream, long fileSize, string contentType);
        Task<Stream> DownloadFileAsync(string objectName); // Add this declaration
        Task DeleteFileAsync(string objectName);
        Task<bool> FileExistsAsync(string objectName);
    }
}
