using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;

namespace Contoso.Documents
{
    /// <summary>
    /// Legacy on-premises integration: a partner drops files onto an SMB share that is backed
    /// by Azure Files.
    /// </summary>
    public class FileShareRepository
    {
        private readonly ShareClient _share;

        public FileShareRepository(ShareServiceClient client, string shareName)
        {
            _share = client.GetShareClient(shareName);
        }

        public async Task InitializeAsync()
        {
            await _share.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        public async Task UploadAsync(string directoryName, string fileName, Stream content)
        {
            ShareDirectoryClient directory = _share.GetRootDirectoryClient()
                .GetSubdirectoryClient(directoryName);

            await directory.CreateIfNotExistsAsync().ConfigureAwait(false);

            ShareFileClient file = directory.GetFileClient(fileName);
            Stream upload = content;
            MemoryStream buffered = null;

            if (!content.CanSeek)
            {
                buffered = new MemoryStream();
                await content.CopyToAsync(buffered).ConfigureAwait(false);
                buffered.Position = 0;
                upload = buffered;
            }

            try
            {
                long length = upload.Length - upload.Position;
                await file.CreateAsync(length).ConfigureAwait(false);
                await file.UploadAsync(upload).ConfigureAwait(false);
            }
            finally
            {
                buffered?.Dispose();
            }
        }

        public async Task<Stream> DownloadAsync(string directoryName, string fileName)
        {
            ShareDirectoryClient directory = _share.GetRootDirectoryClient()
                .GetSubdirectoryClient(directoryName);
            ShareFileClient file = directory.GetFileClient(fileName);

            var buffer = new MemoryStream();
            using ShareFileDownloadInfo download =
                (await file.DownloadAsync().ConfigureAwait(false)).Value;
            await download.Content.CopyToAsync(buffer).ConfigureAwait(false);
            buffer.Position = 0;

            return buffer;
        }

        public async Task<bool> ExistsAsync(string directoryName, string fileName)
        {
            ShareDirectoryClient directory = _share.GetRootDirectoryClient()
                .GetSubdirectoryClient(directoryName);
            ShareFileClient file = directory.GetFileClient(fileName);

            return (await file.ExistsAsync().ConfigureAwait(false)).Value;
        }

        public async Task<bool> DeleteAsync(string directoryName, string fileName)
        {
            ShareDirectoryClient directory = _share.GetRootDirectoryClient()
                .GetSubdirectoryClient(directoryName);
            ShareFileClient file = directory.GetFileClient(fileName);

            return (await file.DeleteIfExistsAsync().ConfigureAwait(false)).Value;
        }

        /// <summary>
        /// Asynchronously lists files in the directory and omits subdirectories.
        /// </summary>
        public async Task<IReadOnlyList<string>> ListAsync(string directoryName)
        {
            ShareDirectoryClient directory = _share.GetRootDirectoryClient()
                .GetSubdirectoryClient(directoryName);

            var names = new List<string>();
            await foreach (ShareFileItem item in directory.GetFilesAndDirectoriesAsync())
            {
                if (!item.IsDirectory)
                {
                    names.Add(item.Name);
                }
            }

            return names;
        }

        /// <summary>
        /// Reads the share quota so the ops dashboard can alert before it fills up.
        /// </summary>
        public async Task<int> GetQuotaInGigabytesAsync()
        {
            ShareProperties properties = (await _share.GetPropertiesAsync().ConfigureAwait(false)).Value;
            return properties.QuotaInGB ?? 0;
        }
    }
}
