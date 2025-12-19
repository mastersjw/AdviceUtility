using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using RemittanceAdviceManager.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace RemittanceAdviceManager.Services.Implementation
{
    public class PrintService : IPrintService
    {
        private readonly ILogger<PrintService> _logger;

        public PrintService(ILogger<PrintService> logger)
        {
            _logger = logger;
        }

        public async Task PrintPdfAsync(string pdfPath)
        {
            try
            {
                if (!File.Exists(pdfPath))
                {
                    _logger.LogError($"PDF file not found: {pdfPath}");
                    throw new FileNotFoundException($"PDF file not found: {pdfPath}");
                }

                // Use shell execute to print PDF with default application
                var psi = new ProcessStartInfo
                {
                    FileName = pdfPath,
                    Verb = "print",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = true
                };

                var process = Process.Start(psi);

                _logger.LogInformation($"Sent PDF to printer: {pdfPath}");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error printing PDF: {pdfPath}");
                throw;
            }
        }

        public async Task PrintMultiplePdfsAsync(List<string> pdfPaths, bool showDialog = true)
        {
            foreach (var pdfPath in pdfPaths)
            {
                await PrintPdfAsync(pdfPath);

                // Add delay to prevent overwhelming print queue
                await Task.Delay(1000);
            }
        }
    }
}
