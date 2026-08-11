using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace TeamSync.Services
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobContainerClient? _containerClient;
        private readonly ILogger<BlobStorageService> _logger;
        private readonly bool _isConfigured;

        public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
        {
            _logger = logger;
            var connectionString = configuration.GetConnectionString("AzureStorageConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                _isConfigured = false;
                _logger.LogWarning("Azure Storage connection string not configured. File uploads will use local storage.");
                _containerClient = null;
            }
            else
            {
                try
                {
                    var blobServiceClient = new BlobServiceClient(connectionString);
                    _containerClient = blobServiceClient.GetBlobContainerClient("task-attachments");
                    _isConfigured = true;
                    _logger.LogInformation("Azure Blob Storage service initialized successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to initialize Azure Blob Storage: {ex.Message}");
                    _isConfigured = false;
                    _containerClient = null;
                }
            }
        }

        public async Task<string> UploadBlobAsync(string containerName, string fileName, Stream fileStream)
        {
            if (!_isConfigured || _containerClient == null)
            {
                throw new InvalidOperationException("Azure Blob Storage is not configured.");
            }

            try
            {
                var blobClient = _containerClient.GetBlobClient(fileName);
                fileStream.Position = 0; // Reset stream position
                await blobClient.UploadAsync(fileStream, overwrite: true);

                _logger.LogInformation($"File {fileName} uploaded to blob storage successfully.");
                return blobClient.Uri.AbsoluteUri;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading file {fileName} to blob storage: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteBlobAsync(string containerName, string fileName)
        {
            if (!_isConfigured || _containerClient == null)
            {
                throw new InvalidOperationException("Azure Blob Storage is not configured.");
            }

            try
            {
                var blobClient = _containerClient.GetBlobClient(fileName);
                await blobClient.DeleteAsync();
                _logger.LogInformation($"File {fileName} deleted from blob storage successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting file {fileName} from blob storage: {ex.Message}");
                throw;
            }
        }

        public async Task<byte[]> DownloadBlobAsync(string containerName, string fileName)
        {
            if (!_isConfigured || _containerClient == null)
            {
                throw new InvalidOperationException("Azure Blob Storage is not configured.");
            }

            try
            {
                var blobClient = _containerClient.GetBlobClient(fileName);
                var download = await blobClient.DownloadAsync();
                using (var ms = new MemoryStream())
                {
                    await download.Value.Content.CopyToAsync(ms);
                    _logger.LogInformation($"File {fileName} downloaded from blob storage successfully.");
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error downloading file {fileName} from blob storage: {ex.Message}");
                throw;
            }
        }

        public bool IsConfigured()
        {
            return _isConfigured;
        }
    }
}
