using Microsoft.Extensions.Options;
using NasBridgeApi.Models;
using SharpCifs.Smb;

namespace NasBridgeApi.Services
{
    public class NasService
    {
        private readonly string _baseUrl;
        private readonly NtlmPasswordAuthentication _auth;
        private readonly NasSettings _settings;

        public NasService(IOptions<NasSettings> options)
        {
            _settings = options.Value;
            _baseUrl = _settings.BaseUrl.EndsWith("/") ? _settings.BaseUrl : _settings.BaseUrl + "/";
            _auth = new NtlmPasswordAuthentication(null, _settings.Username, _settings.Password);
        }

        public async Task<List<NasEntry>> ListFilesAsync(string relativePath = "")
        {
            string path = Path.Combine(_baseUrl, relativePath);
            path = path.EndsWith("/") ? path : path + "/";
            var dir = new SmbFile(path, _auth);

            if (!dir.Exists())
                throw new DirectoryNotFoundException($"NAS path not found: {path}");

            var files = await dir.ListFilesAsync();

            return files.Select(f =>
            {
                var isFile = f.IsFile();
                var size = isFile ? f.Length() : 0;

                return new NasEntry
                {
                    Name = f.GetName().TrimEnd('/'),
                    Type = f.IsDirectory() ? "Folder" : "File",
                    Size = size,
                    FormattedSize = isFile ? FormatBytes(size) : "",
                    DateCreated = f.CreateTime() > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(f.CreateTime()).DateTime : null,
                    DateModified = f.LastModified() > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(f.LastModified()).DateTime : null,
                    FullPath = f.GetPath()
                };
            }).OrderBy(e => e.Type).ThenBy(e => e.Name).ToList();
        }

        public async Task UploadFileAsync(string relativePath, Stream stream, bool overwrite = true)
        {
            string fullPath = Path.Combine(_baseUrl, relativePath).Replace("\\", "/");

            var directory = GetSmbDirectory(fullPath) ?? "";
            var originalFileName = Path.GetFileNameWithoutExtension(fullPath);
            var extension = Path.GetExtension(fullPath);

            var targetPath = fullPath;
            var file = new SmbFile(targetPath, _auth);

            // Ensure parent directory exists
            var parent = new SmbFile(file.GetParent(), _auth);
            if (!parent.Exists())
            {
                parent.Mkdirs(); // recursively create missing folders
            }

            if (file.Exists())
            {
                if (overwrite)
                {
                    await file.DeleteAsync(); // overwrite
                }
                else
                {
                    int counter = 1;
                    string newFileName;
                    do
                    {
                        newFileName = $"{originalFileName}_{counter}{extension}";
                        targetPath = Path.Combine(directory, newFileName).Replace("\\", "/");
                        file = new SmbFile(targetPath, _auth);
                        counter++;
                    }
                    while (file.Exists());
                }
            }

            using var outStream = await file.GetOutputStreamAsync();
            await stream.CopyToAsync(outStream);
            outStream.Flush();
        }

        public async Task<Stream> DownloadFileAsync(string relativePath)
        {
            string path = Path.Combine(_baseUrl, relativePath);
            var file = new SmbFile(path, _auth);

            if (!file.Exists())
                throw new FileNotFoundException($"File not found: {relativePath}");

            return await file.GetInputStreamAsync();
        }

        public async Task DeleteFileAsync(string relativePath)
        {
            string path = Path.Combine(_baseUrl, relativePath);
            var file = new SmbFile(path, _auth);

            if (file.Exists())
                await file.DeleteAsync();
        }
        public async Task<bool> FileExistsAsync(string relativePath)
        {
            string path = Path.Combine(_baseUrl, relativePath).Replace("\\", "/");
            var file = new SmbFile(path, _auth);
            return file.Exists();
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return $"{kb:F1} KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return $"{mb:F1} MB";
            double gb = mb / 1024.0;
            return $"{gb:F1} GB";
        }
        private string GetSmbDirectory(string smbPath)
        {
            if (string.IsNullOrEmpty(smbPath)) return smbPath;

            smbPath = smbPath.Replace("\\", "/");

            int lastSlashIndex = smbPath.LastIndexOf('/');
            if (lastSlashIndex <= "smb://".Length)
                return smbPath; // no deeper folder

            return smbPath.Substring(0, lastSlashIndex);
        }
    }
}
