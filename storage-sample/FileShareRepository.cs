using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;

namespace Contoso.Documents
{
    public class FileShareRepository
    {
        private readonly ShareClient _share;

        public FileShareRepository(ShareServiceClient client, string shareName)
        {
            _share = client.GetShareClient(shareName);
        }

        public async Task InitializeAsync() =>
            await _share.CreateIfNotExistsAsync().ConfigureAwait(false);

        public async Task UploadAsync(string directoryName, string fileName, Stream content)
        {
            ShareDirectoryClient directory =
                _share.GetRootDirectoryClient().GetSubdirectoryClient(directoryName);
            await directory.CreateIfNotExistsAsync().ConfigureAwait(false);
            ShareFileClient file = directory.GetFileClient(fileName);

            MemoryStream buffer = null;
            Stream upload = content;
            if (!content.CanSeek)
            {
                buffer = new MemoryStream();
                await content.CopyToAsync(buffer).ConfigureAwait(false);
                buffer.Position = 0;
                upload = buffer;
            }

            try
            {
                await file.CreateAsync(upload.Length - upload.Position).ConfigureAwait(false);
                await file.UploadAsync(upload).ConfigureAwait(false);
            }
            finally
            {
                buffer?.Dispose();
            }
        }

        public async Task<Stream> DownloadAsync(string directoryName, string fileName)
        {
            ShareFileClient file = GetFile(directoryName, fileName);
            var buffer = new MemoryStream();
            ShareFileDownloadInfo download = (await file.DownloadAsync().ConfigureAwait(false)).Value;
            await download.Content.CopyToAsync(buffer).ConfigureAwait(false);
            buffer.Position = 0;
            return buffer;
        }

        public async Task<bool> ExistsAsync(string directoryName, string fileName) =>
            (await GetFile(directoryName, fileName).ExistsAsync().ConfigureAwait(false)).Value;

        public async Task<bool> DeleteAsync(string directoryName, string fileName) =>
            (await GetFile(directoryName, fileName).DeleteIfExistsAsync().ConfigureAwait(false)).Value;

        public async Task<IReadOnlyList<string>> ListAsync(string directoryName)
        {
            ShareDirectoryClient directory =
                _share.GetRootDirectoryClient().GetSubdirectoryClient(directoryName);
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

        public async Task<int> GetQuotaInGigabytesAsync()
        {
            ShareProperties properties = (await _share.GetPropertiesAsync().ConfigureAwait(false)).Value;
            return properties.QuotaInGB ?? 0;
        }

        private ShareFileClient GetFile(string directoryName, string fileName) =>
            _share.GetRootDirectoryClient()
                .GetSubdirectoryClient(directoryName)
                .GetFileClient(fileName);
    }
}
