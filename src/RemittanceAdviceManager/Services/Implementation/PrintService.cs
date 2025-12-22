using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using RemittanceAdviceManager.Services.Interfaces;
using Microsoft.Extensions.Logging;
using PdfiumViewer;
using System.Windows.Interop;

namespace RemittanceAdviceManager.Services.Implementation
{
    public class PrintService : IPrintService
    {
        private readonly ILogger<PrintService> _logger;

        public PrintService(ILogger<PrintService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> PrintPdfAsync(string pdfPath, bool showDialog = true)
        {
            // Get the window handle on the UI thread before Task.Run
            IntPtr windowHandle = IntPtr.Zero;
            if (showDialog)
            {
                try
                {
                    // Access WPF objects on the UI thread
                    var activeWindow = System.Windows.Application.Current?.Windows
                        .OfType<System.Windows.Window>()
                        .FirstOrDefault(w => w.IsActive);

                    if (activeWindow == null)
                    {
                        activeWindow = System.Windows.Application.Current?.MainWindow;
                    }

                    if (activeWindow != null)
                    {
                        var helper = new WindowInteropHelper(activeWindow);
                        windowHandle = helper.Handle;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get window handle for print dialog owner");
                }
            }

            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(pdfPath))
                    {
                        var errorMsg = $"PDF file not found at: {pdfPath}\n\nThe file may have been moved or deleted.";
                        _logger.LogError($"PDF file not found: {pdfPath}");
                        throw new FileNotFoundException(errorMsg);
                    }

                    _logger.LogInformation($"Attempting to print: {pdfPath}");

                    // Use PdfiumViewer to print the PDF reliably
                    using (var document = PdfDocument.Load(pdfPath))
                    {
                        using (var printDocument = document.CreatePrintDocument())
                        {
                            // Configure default print settings
                            printDocument.PrinterSettings.PrinterName = new PrinterSettings().PrinterName; // Use default printer
                            printDocument.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

                            if (showDialog)
                            {
                                // Show print dialog to let user choose printer, copies, etc.
                                using (var printDialog = new PrintDialog())
                                {
                                    printDialog.Document = printDocument;
                                    printDialog.AllowSomePages = true;
                                    printDialog.AllowSelection = false;
                                    printDialog.AllowCurrentPage = false;

                                    // Use the window handle we got from the UI thread
                                    System.Windows.Forms.IWin32Window owner = null;
                                    if (windowHandle != IntPtr.Zero)
                                    {
                                        owner = new Win32Window(windowHandle);
                                    }

                                    var dialogResult = owner != null
                                        ? printDialog.ShowDialog(owner)
                                        : printDialog.ShowDialog();

                                    if (dialogResult == System.Windows.Forms.DialogResult.OK)
                                    {
                                        _logger.LogInformation($"User selected printer: {printDocument.PrinterSettings.PrinterName}");
                                        printDocument.Print();
                                        _logger.LogInformation($"Successfully sent PDF to printer: {pdfPath}");
                                        return true;
                                    }
                                    else
                                    {
                                        _logger.LogInformation($"User cancelled print dialog for: {pdfPath}");
                                        return false;
                                    }
                                }
                            }
                            else
                            {
                                // Silent print without dialog
                                printDocument.PrintController = new StandardPrintController();
                                _logger.LogInformation($"Sending to printer: {printDocument.PrinterSettings.PrinterName}");
                                printDocument.Print();
                                _logger.LogInformation($"Successfully sent PDF to printer: {pdfPath}");
                                return true;
                            }
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error printing PDF: {pdfPath}");

                    var errorMsg = $"Failed to print PDF: {Path.GetFileName(pdfPath)}\n\n" +
                                   $"Possible causes:\n" +
                                   $"- No printer configured\n" +
                                   $"- Printer is offline\n" +
                                   $"- PDF file is corrupted\n\n" +
                                   $"Error: {ex.Message}";

                    throw new InvalidOperationException(errorMsg, ex);
                }
            });
        }

        public async Task<int> PrintMultiplePdfsAsync(List<string> pdfPaths, bool showDialog = false)
        {
            // For multiple files, default to no dialog (but can be overridden)
            // Show dialog only for first file if requested
            bool showDialogForFile = showDialog;
            int successCount = 0;

            foreach (var pdfPath in pdfPaths)
            {
                try
                {
                    bool success = await PrintPdfAsync(pdfPath, showDialogForFile);

                    if (!success)
                    {
                        _logger.LogInformation("Print job cancelled by user, stopping batch print");
                        break; // User cancelled, stop printing
                    }

                    successCount++;

                    // Only show dialog for first file, then use same settings for rest
                    showDialogForFile = false;

                    // Add delay to prevent overwhelming print queue
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error printing file: {pdfPath}");
                    // Continue with next file instead of stopping entire batch
                }
            }

            return successCount;
        }
    }

    /// <summary>
    /// Helper class to convert WPF window handle to IWin32Window for Windows Forms dialogs
    /// </summary>
    internal class Win32Window : System.Windows.Forms.IWin32Window
    {
        private readonly IntPtr _handle;

        public Win32Window(IntPtr handle)
        {
            _handle = handle;
        }

        public IntPtr Handle => _handle;
    }
}
