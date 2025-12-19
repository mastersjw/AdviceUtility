using System;

namespace RemittanceAdviceManager.Models
{
    public class RemittanceFile
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime DownloadedDate { get; set; }
        public FileStatus Status { get; set; }
        public string? AdviceNumber { get; set; }
        public bool IsPrinted { get; set; }
        public DateTime? UploadedDate { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // UI-only property (not stored in database)
        public bool IsSelected { get; set; }
    }

    public enum FileStatus
    {
        Downloaded,
        Altered,
        Uploaded,
        Error
    }
}
