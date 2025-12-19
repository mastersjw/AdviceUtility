namespace RemittanceAdviceManager.Models
{
    public class UploadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? RemittanceNumber { get; set; }
        public int ReconciledClaims { get; set; }
        public int UnreconciledClaims { get; set; }
        public string FileName { get; set; } = string.Empty;
    }
}
