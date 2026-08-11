using System.IO;
using System.Threading.Tasks;

namespace TeamSync.Services
{
    public interface IBlobStorageService
    {
        /// <summary>
        /// Uploads a file to Azure Blob Storage
        /// </summary>
        /// <param name="containerName">Name of the blob container</param>
        /// <param name="fileName">Name of the file in blob storage</param>
        /// <param name="fileStream">File stream to upload</param>
        /// <returns>Uri of the uploaded blob</returns>
        Task<string> UploadBlobAsync(string containerName, string fileName, Stream fileStream);

        /// <summary>
        /// Deletes a blob from Azure Blob Storage
        /// </summary>
        /// <param name="containerName">Name of the blob container</param>
        /// <param name="fileName">Name of the file to delete</param>
        Task DeleteBlobAsync(string containerName, string fileName);

        /// <summary>
        /// Checks if Azure Blob Storage is configured
        /// </summary>
        /// <returns>True if connection string is available</returns>
        bool IsConfigured();
    }
}
