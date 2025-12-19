using System.Threading.Tasks;

namespace RemittanceAdviceManager.Services.Interfaces
{
    public interface IPdfProcessingService
    {
        Task<bool> AlterPdfAsync(string sourcePath, string destinationPath);
        Task<string> ExtractTextAsync(string pdfPath);
        Task<bool> ValidatePdfAsync(string pdfPath);
        Task<bool> MergePdfsAsync(string[] sourcePaths, string destinationPath);
    }
}
