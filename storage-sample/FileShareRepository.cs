using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.File;

namespace Contoso.Documents
{
    /// <summary>
    /// Legacy on-premises integration: a partner drops files onto an SMB share that is backed
    /// by Azure Files.
    /// </summary>
    public class FileShareRepository
    {
        private readonly CloudFileShare _share;

        public FileShareRepository(CloudFileClient client, string shareName)
        {
            _share = client.GetShareReference(shareName);
        }

        public async Task InitializeAsync()
        {
            await _share.CreateIfNotExistsAsync().ConfigureAwait(false);
        }

        public async Task UploadAsync(string directoryName, string fileName, Stream content)
        {
            CloudFileDirectory root = _share.GetRootDirectoryReference();
            CloudFileDirectory directory = root.GetDirectoryReference(directoryName);

            await directory.CreateIfNotExistsAsync().ConfigureAwait(false);

            CloudFile file = directory.GetFileReference(fileName);
            await file.UploadFromStreamAsync(content).ConfigureAwait(false);
        }

        public async Task<Stream> DownloadAsync(string directoryName, string fileName)
        {
            CloudFileDirectory root = _share.GetRootDirectoryReference();
            CloudFileDirectory directory = root.GetDirectoryReference(directoryName);
            CloudFile file = directory.GetFileReference(fileName);

            var buffer = new MemoryStream();
            await file.DownloadToStreamAsync(buffer).ConfigureAwait(false);
            buffer.Position = 0;

            return buffer;
        }

        public async Task<bool> ExistsAsync(string directoryName, string fileName)
        {
            CloudFileDirectory root = _share.GetRootDirectoryReference();
            CloudFileDirectory directory = root.GetDirectoryReference(directoryName);
            CloudFile file = directory.GetFileReference(fileName);

            return await file.ExistsAsync().ConfigureAwait(false);
        }

        public async Task<bool> DeleteAsync(string directoryName, string fileName)
        {
            CloudFileDirectory root = _share.GetRootDirectoryReference();
            CloudFileDirectory directory = root.GetDirectoryReference(directoryName);
            CloudFile file = directory.GetFileReference(fileName);

            return await file.DeleteIfExistsAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Segmented directory listing, mirroring the blob listing shape.
        /// </summary>
        public async Task<IReadOnlyList<string>> ListAsync(string directoryName)
        {
            CloudFileDirectory root = _share.GetRootDirectoryReference();
            CloudFileDirectory directory = root.GetDirectoryReference(directoryName);

            var names = new List<string>();
            FileContinuationToken token = null;

            do
            {
                FileResultSegment segment = await directory
                    .ListFilesAndDirectoriesSegmentedAsync(token)
                    .ConfigureAwait(false);

                foreach (IListFileItem item in segment.Results)
                {
                    var file = item as CloudFile;
                    if (file != null)
                    {
                        names.Add(file.Name);
                    }
                }

                token = segment.ContinuationToken;
            }
            while (token != null);

            return names;
        }

        /// <summary>
        /// Reads the share quota so the ops dashboard can alert before it fills up.
        /// </summary>
        public async Task<int> GetQuotaInGigabytesAsync()
        {
            await _share.FetchAttributesAsync().ConfigureAwait(false);
            return _share.Properties.Quota ?? 0;
        }
    }
}
