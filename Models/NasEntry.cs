namespace NasBridgeApi.Models
{
    public class NasEntry
    {
        public string Name { get; set; }
        public string Type { get; set; }  // "File" or "Folder"
        public long Size { get; set; } // in bytes, 0 for folders
        public string FormattedSize { get; set; } // "2.4 MB"
        public DateTime? DateCreated { get; set; }
        public DateTime? DateModified { get; set; }
        public string FullPath { get; set; }
    }
}
