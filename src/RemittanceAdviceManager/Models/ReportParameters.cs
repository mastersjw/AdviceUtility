using System;

namespace RemittanceAdviceManager.Models
{
    public class ReportParameters
    {
        public DateTime AdviceDate { get; set; }
        public string VendorNum { get; set; } = string.Empty;
        public ReportFormat Format { get; set; } = ReportFormat.PDF;
    }

    public enum ReportFormat
    {
        PDF,
        Excel,
        Word
    }
}
