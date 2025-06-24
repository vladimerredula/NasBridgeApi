namespace NasBridgeApi.Models
{
    public class FileUploadRequest
    {
        public IFormFile File { get; set; }
        public string? RelativePath { get; set; }
        public bool Overwrite { get; set; } = true;
    }
}
