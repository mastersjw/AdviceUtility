using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RemittanceAdviceManager.Models;
using RemittanceAdviceManager.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

namespace RemittanceAdviceManager.ViewModels
{
    public partial class DownloadReportsViewModel : ObservableObject
    {
        private readonly IWebDbAuthenticationService _authService;
        private readonly IReportDownloadService _reportService;
        private readonly IPrintService _printService;
        private readonly ILogger<DownloadReportsViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<DateTime> _availableDates = new();

        [ObservableProperty]
        private ObservableCollection<string> _vendorNumbers = new();

        [ObservableProperty]
        private DateTime _selectedAdviceDate = DateTime.Now;

        [ObservableProperty]
        private string _selectedVendorNum = string.Empty;

        [ObservableProperty]
        private byte[]? _downloadedReportData;

        [ObservableProperty]
        private string? _reportPreviewPath;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private bool _isAuthenticated;

        public DownloadReportsViewModel(
            IWebDbAuthenticationService authService,
            IReportDownloadService reportService,
            IPrintService printService,
            ILogger<DownloadReportsViewModel> logger)
        {
            _authService = authService;
            _reportService = reportService;
            _printService = printService;
            _logger = logger;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // Check if already authenticated
            IsAuthenticated = await _authService.IsAuthenticatedAsync();

            if (IsAuthenticated)
            {
                await LoadParametersAsync();
            }
        }

        [RelayCommand]
        private async Task LoadParametersAsync()
        {
            try
            {
                StatusMessage = "Loading parameters...";

                var dates = await _reportService.GetAvailableAdviceDatesAsync();
                AvailableDates = new ObservableCollection<DateTime>(dates);

                var vendors = await _reportService.GetVendorNumbersAsync();
                VendorNumbers = new ObservableCollection<string>(vendors);

                if (AvailableDates.Count > 0)
                    SelectedAdviceDate = AvailableDates[0];

                if (VendorNumbers.Count > 0)
                    SelectedVendorNum = VendorNumbers[0];

                StatusMessage = "Parameters loaded";
                _logger.LogInformation("Report parameters loaded");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading parameters: {ex.Message}";
                _logger.LogError(ex, "Error loading report parameters");
            }
        }

        [RelayCommand(CanExecute = nameof(CanDownloadReport))]
        private async Task DownloadReportAsync()
        {
            try
            {
                IsDownloading = true;
                StatusMessage = "Downloading report...";

                var parameters = new ReportParameters
                {
                    AdviceDate = SelectedAdviceDate,
                    VendorNum = SelectedVendorNum,
                    Format = ReportFormat.PDF
                };

                DownloadedReportData = await _reportService.DownloadProviderCheckTotalsAsync(parameters);

                // Save to temp file for preview
                ReportPreviewPath = Path.Combine(Path.GetTempPath(),
                    $"ProviderCheckTotals_{SelectedAdviceDate:yyyyMMdd}_{SelectedVendorNum}.pdf");

                await File.WriteAllBytesAsync(ReportPreviewPath, DownloadedReportData);

                StatusMessage = $"Report downloaded ({DownloadedReportData.Length} bytes)";
                _logger.LogInformation($"Report downloaded: {ReportPreviewPath}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error downloading report: {ex.Message}";
                _logger.LogError(ex, "Error downloading report");
            }
            finally
            {
                IsDownloading = false;
            }
        }

        private bool CanDownloadReport() =>
            !IsDownloading &&
            SelectedAdviceDate != default &&
            !string.IsNullOrEmpty(SelectedVendorNum);

        [RelayCommand(CanExecute = nameof(HasDownloadedReport))]
        private async Task PrintReportAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(ReportPreviewPath))
                    return;

                StatusMessage = "Printing report...";
                await _printService.PrintPdfAsync(ReportPreviewPath);

                StatusMessage = "Report sent to printer";
                _logger.LogInformation("Report printed");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error printing report: {ex.Message}";
                _logger.LogError(ex, "Error printing report");
            }
        }

        private bool HasDownloadedReport() => !string.IsNullOrEmpty(ReportPreviewPath);

        [RelayCommand(CanExecute = nameof(HasDownloadedReport))]
        private async Task SaveReportAsync()
        {
            try
            {
                if (DownloadedReportData == null)
                    return;

                // In a real app, show SaveFileDialog here
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fileName = $"ProviderCheckTotals_{SelectedAdviceDate:yyyyMMdd}_{SelectedVendorNum}.pdf";
                var savePath = Path.Combine(desktopPath, fileName);

                await File.WriteAllBytesAsync(savePath, DownloadedReportData);

                StatusMessage = $"Report saved to: {savePath}";
                _logger.LogInformation($"Report saved: {savePath}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving report: {ex.Message}";
                _logger.LogError(ex, "Error saving report");
            }
        }
    }
}
