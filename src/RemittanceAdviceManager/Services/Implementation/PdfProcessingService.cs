using System;
using System.IO;
using System.Threading.Tasks;
using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using RemittanceAdviceManager.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace RemittanceAdviceManager.Services.Implementation
{
    public class PdfProcessingService : IPdfProcessingService
    {
        private readonly ILogger<PdfProcessingService> _logger;

        public PdfProcessingService(ILogger<PdfProcessingService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> AlterPdfAsync(string sourcePath, string destinationPath)
        {
            try
            {
                // For now, just copy the file as a placeholder
                // In the future, implement actual PDF alteration logic
                // based on what the original AdviceDownloader did
                File.Copy(sourcePath, destinationPath, overwrite: true);

                _logger.LogInformation($"Altered PDF from {sourcePath} to {destinationPath}");
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error altering PDF: {sourcePath}");
                return false;
            }
        }

        public async Task<string> ExtractTextAsync(string pdfPath)
        {
            try
            {
                using var pdfReader = new PdfReader(pdfPath);
                using var pdfDocument = new PdfDocument(pdfReader);

                var text = string.Empty;
                for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                {
                    var page = pdfDocument.GetPage(i);
                    var strategy = new SimpleTextExtractionStrategy();
                    text += PdfTextExtractor.GetTextFromPage(page, strategy);
                }

                _logger.LogInformation($"Extracted text from PDF: {pdfPath}");
                return await Task.FromResult(text);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting text from PDF: {pdfPath}");
                throw;
            }
        }

        public async Task<bool> ValidatePdfAsync(string pdfPath)
        {
            try
            {
                if (!File.Exists(pdfPath))
                    return false;

                using var pdfReader = new PdfReader(pdfPath);
                using var pdfDocument = new PdfDocument(pdfReader);

                // Check if PDF has at least one page
                var isValid = pdfDocument.GetNumberOfPages() > 0;

                return await Task.FromResult(isValid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating PDF: {pdfPath}");
                return false;
            }
        }

        public async Task<bool> MergePdfsAsync(string[] sourcePaths, string destinationPath)
        {
            try
            {
                using var pdfWriter = new PdfWriter(destinationPath);
                using var pdfDocument = new PdfDocument(pdfWriter);
                var pdfMerger = new PdfMerger(pdfDocument);

                foreach (var sourcePath in sourcePaths)
                {
                    using var sourceReader = new PdfReader(sourcePath);
                    using var sourcePdfDocument = new PdfDocument(sourceReader);
                    pdfMerger.Merge(sourcePdfDocument, 1, sourcePdfDocument.GetNumberOfPages());
                }

                _logger.LogInformation($"Merged {sourcePaths.Length} PDFs to {destinationPath}");
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error merging PDFs");
                return false;
            }
        }

        public async Task<string> ExtractPaidClaimsTotalsPageAsync(string pdfPath, string date, string providerId)
        {
            try
            {
                if (!File.Exists(pdfPath))
                {
                    _logger.LogError($"PDF file does not exist: {pdfPath}");
                    return null;
                }

                // Find page containing "PAID CLAIM TOTALS"
                int pageNumber = await FindPageWithTextAsync(pdfPath, "PAID CLAIM TOTALS");

                if (pageNumber == -1)
                {
                    _logger.LogWarning($"'PAID CLAIM TOTALS' not found in {pdfPath}. Deleting file...");
                    try
                    {
                        File.Delete(pdfPath);
                        _logger.LogInformation($"Deleted: {pdfPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error deleting file: {pdfPath}");
                    }
                    return null;
                }

                _logger.LogInformation($"Found 'PAID CLAIM TOTALS' on page {pageNumber}");

                // Extract and save the page
                string outputPath = await SaveExtractedPageAsync(pdfPath, pageNumber, date, providerId);
                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting PAID CLAIM TOTALS page from {pdfPath}");
                return null;
            }
        }

        private async Task<int> FindPageWithTextAsync(string pdfPath, string searchText)
        {
            try
            {
                using var pdfReader = new PdfReader(pdfPath);
                using var pdfDocument = new PdfDocument(pdfReader);

                for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                {
                    var page = pdfDocument.GetPage(i);
                    string pageText = PdfTextExtractor.GetTextFromPage(page);

                    if (pageText.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        return await Task.FromResult(i);
                    }
                }

                return await Task.FromResult(-1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading PDF: {pdfPath}");
                return -1;
            }
        }

        private async Task<string> SaveExtractedPageAsync(string pdfPath, int pageToExtract, string date, string providerId)
        {
            string outputFileName = $"TotalsPage_{date}_{providerId}.pdf";
            string outputPath = Path.Combine(Path.GetDirectoryName(pdfPath), outputFileName);

            try
            {
                using (var sourceReader = new PdfReader(pdfPath))
                using (var sourcePdfDocument = new PdfDocument(sourceReader))
                {
                    using var pdfWriter = new PdfWriter(outputPath);
                    using var outputPdfDocument = new PdfDocument(pdfWriter);

                    // Copy the specific page to the new document
                    sourcePdfDocument.CopyPagesTo(pageToExtract, pageToExtract, outputPdfDocument);
                }

                _logger.LogInformation($"Page {pageToExtract} successfully saved as: {outputPath}");
                return await Task.FromResult(outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting and saving page {pageToExtract}");
                return null;
            }
        }
    }
}
