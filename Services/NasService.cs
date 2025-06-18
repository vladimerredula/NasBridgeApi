using Microsoft.Extensions.Options;
using NasBridgeApi.Models;
using SharpCifs.Smb;
using System.Drawing;

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
            string path = _baseUrl + relativePath;
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

        public async Task UploadFileAsync(string relativePath, Stream stream)
        {
            string path = _baseUrl + relativePath;
            var file = new SmbFile(path, _auth);

            if (file.Exists())
                await file.DeleteAsync(); // overwrite

            using var outStream = await file.GetOutputStreamAsync();
            await stream.CopyToAsync(outStream);
            outStream.Flush();
        }

        public async Task<Stream> DownloadFileAsync(string relativePath)
        {
            string path = _baseUrl + relativePath;
            var file = new SmbFile(path, _auth);

            if (!file.Exists())
                throw new FileNotFoundException($"File not found: {relativePath}");

            return await file.GetInputStreamAsync();
        }

        public async Task DeleteFileAsync(string relativePath)
        {
            string path = _baseUrl + relativePath;
            var file = new SmbFile(path, _auth);

            if (file.Exists())
                await file.DeleteAsync();
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
    }
}
