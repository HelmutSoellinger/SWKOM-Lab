using System.IO;
using System.Threading.Tasks;

namespace DMSystem.Minio
{
    public interface IFileStorageService
    {
        Task InitializeBucketAsync();
        Task UploadFileAsync(string objectName, Stream fileStream, long fileSize, string contentType);
        Task<Stream> DownloadFileAsync(string objectName);
        Task DeleteFileAsync(string objectName);
        Task<bool> FileExistsAsync(string objectName);
    }
}
