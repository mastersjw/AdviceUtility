using System.Threading.Tasks;

namespace RemittanceAdviceManager.Services.Interfaces
{
    public interface IPdfProcessingService
    {
        Task<bool> AlterPdfAsync(string sourcePath, string destinationPath);
        Task<string> ExtractTextAsync(string pdfPath);
        Task<bool> ValidatePdfAsync(string pdfPath);
        Task<bool> MergePdfsAsync(string[] sourcePaths, string destinationPath);

        /// <summary>
        /// Extracts the "PAID CLAIM TOTALS" page from a PDF and saves it to a separate file
        /// </summary>
        /// <param name="pdfPath">Path to the source PDF</param>
        /// <param name="date">Date string for output filename</param>
        /// <param name="providerId">Provider ID for output filename</param>
        /// <returns>Path to the extracted page PDF, or null if not found</returns>
        Task<string> ExtractPaidClaimsTotalsPageAsync(string pdfPath, string date, string providerId);
    }
}
