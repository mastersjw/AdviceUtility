using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RemittanceAdviceManager.Models;
using RemittanceAdviceManager.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RemittanceAdviceManager.ViewModels
{
    public partial class DownloadReportsViewModel : ObservableObject
    {
        private readonly IWebDbAuthenticationService _authService;
        private readonly IReportDownloadService _reportService;
        private readonly IPrintService _printService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DownloadReportsViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<DateTime> _availableDates = new();

        [ObservableProperty]
        private ObservableCollection<ProviderNumber> _providerNumbers = new();

        [ObservableProperty]
        private ObservableCollection<DownloadedReport> _downloadedReports = new();

        [ObservableProperty]
        private DateTime _selectedAdviceDate = DateTime.Now;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        [NotifyCanExecuteChangedFor(nameof(LoadParametersCommand))]
        [NotifyCanExecuteChangedFor(nameof(DownloadReportCommand))]
        private bool _isAuthenticated;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        private bool _isAuthenticating;

        [ObservableProperty]
        private WebDbCredentials _credentials = new();

        [ObservableProperty]
        private string _downloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        public DownloadReportsViewModel(
            IWebDbAuthenticationService authService,
            IReportDownloadService reportService,
            IPrintService printService,
            IConfiguration configuration,
            ILogger<DownloadReportsViewModel> logger)
        {
            _authService = authService;
            _reportService = reportService;
            _printService = printService;
            _configuration = configuration;
            _logger = logger;

            // Initialize provider numbers
            var providerNumbersList = new[] {
                "393261", "393328", "393497", "800527", "1713651", "1713660", "1713668", "1713686",
                "1713699", "1713702", "1713725", "1713736", "1713738", "1713764", "1713770", "1713777",
                "1713803", "1713804", "1713816", "1713838", "1713842", "1713855", "1713881", "1713889",
                "1713894", "1713940", "1713946", "1713957", "1730963", "1730974", "1730976", "1731002",
                "1731008", "1731015", "1731041", "1731042", "1731054", "1731076", "1731080", "1731093"
            };

            foreach (var number in providerNumbersList)
            {
                var providerNumber = new ProviderNumber { Number = number, IsSelected = false };
                providerNumber.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ProviderNumber.IsSelected))
                    {
                        DownloadReportCommand.NotifyCanExecuteChanged();
                    }
                };
                ProviderNumbers.Add(providerNumber);
            }

            // Subscribe to Credentials property changes to update Login button
            Credentials.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WebDbCredentials.Username))
                {
                    LoginCommand.NotifyCanExecuteChanged();
                }
            };

            // Subscribe to authentication state changes from the service
            _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;

            _ = InitializeAsync();
        }

        private void OnAuthenticationStateChanged(object? sender, bool isAuthenticated)
        {
            IsAuthenticated = isAuthenticated;
            if (isAuthenticated)
            {
                StatusMessage = "Authenticated successfully";
                _ = LoadParametersAsync();
            }
            else
            {
                StatusMessage = "Logged out";
            }
        }

        private async Task InitializeAsync()
        {
            // Load existing reports from the download folder
            LoadExistingReports();

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
                // Check authentication status first (in case user logged in on another tab)
                IsAuthenticated = await _authService.IsAuthenticatedAsync();

                if (!IsAuthenticated)
                {
                    StatusMessage = "Please log in first";
                    return;
                }

                StatusMessage = "Loading parameters...";

                var dates = await _reportService.GetAvailableAdviceDatesAsync();
                AvailableDates = new ObservableCollection<DateTime>(dates);

                if (AvailableDates.Count > 0)
                    SelectedAdviceDate = AvailableDates[0];

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

                var selectedProviders = ProviderNumbers.Where(p => p.IsSelected).ToList();

                if (selectedProviders.Count == 0)
                {
                    StatusMessage = "No providers selected";
                    return;
                }

                StatusMessage = $"Downloading reports for {selectedProviders.Count} provider(s)...";

                // Ensure download folder exists
                if (!Directory.Exists(DownloadFolder))
                {
                    Directory.CreateDirectory(DownloadFolder);
                }

                int downloadedCount = 0;

                // Loop through all selected providers
                foreach (var provider in selectedProviders)
                {
                    try
                    {
                        StatusMessage = $"Downloading report {downloadedCount + 1} of {selectedProviders.Count} (Provider: {provider.Number})...";

                        var parameters = new ReportParameters
                        {
                            AdviceDate = SelectedAdviceDate,
                            VendorNum = provider.Number,
                            Format = ReportFormat.PDF
                        };

                        var reportData = await _reportService.DownloadProviderCheckTotalsAsync(parameters);

                        // Save to selected download folder (using MM_dd_yyyy date format)
                        var fileName = $"ProviderCheckTotals_{SelectedAdviceDate:MM_dd_yyyy}_{provider.Number}.pdf";
                        var savePath = Path.Combine(DownloadFolder, fileName);

                        await File.WriteAllBytesAsync(savePath, reportData);

                        downloadedCount++;
                        _logger.LogInformation($"Report downloaded and saved: {savePath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error downloading report for provider {provider.Number}");
                        StatusMessage = $"Error downloading report for provider {provider.Number}: {ex.Message}";
                        await Task.Delay(1000);
                    }
                }

                // Refresh the list to show all PDFs including newly downloaded ones
                LoadExistingReports();

                StatusMessage = $"Successfully downloaded {downloadedCount} of {selectedProviders.Count} report(s)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error downloading reports: {ex.Message}";
                _logger.LogError(ex, "Error downloading reports");
            }
            finally
            {
                IsDownloading = false;
            }
        }

        private bool CanDownloadReport() =>
            IsAuthenticated &&
            !IsDownloading &&
            SelectedAdviceDate != default &&
            ProviderNumbers.Any(p => p.IsSelected);

        [RelayCommand(CanExecute = nameof(CanLogin))]
        private async Task LoginAsync()
        {
            try
            {
                IsAuthenticating = true;
                StatusMessage = "Authenticating...";

                var success = await _authService.AuthenticateAsync(Credentials);

                if (success)
                {
                    IsAuthenticated = true;
                    StatusMessage = $"Successfully authenticated as {Credentials.Username}";
                    _logger.LogInformation($"Successfully authenticated as {Credentials.Username}");

                    await LoadParametersAsync();
                }
                else
                {
                    StatusMessage = "Authentication failed. Please check your credentials.";
                    _logger.LogWarning("Authentication failed");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Authentication error: {ex.Message}";
                _logger.LogError(ex, "Error during authentication");
            }
            finally
            {
                IsAuthenticating = false;
            }
        }

        private bool CanLogin() => !IsAuthenticating && !string.IsNullOrEmpty(Credentials.Username);

        [RelayCommand]
        private void CheckAllProviders()
        {
            foreach (var provider in ProviderNumbers)
            {
                provider.IsSelected = true;
            }
            StatusMessage = $"Selected all {ProviderNumbers.Count} providers";
        }

        [RelayCommand]
        private void UncheckAllProviders()
        {
            foreach (var provider in ProviderNumbers)
            {
                provider.IsSelected = false;
            }
            StatusMessage = "Unselected all providers";
        }

        [RelayCommand]
        private void CheckAllReports()
        {
            foreach (var report in DownloadedReports)
            {
                report.IsSelected = true;
            }
            StatusMessage = $"Selected all {DownloadedReports.Count} reports";
        }

        [RelayCommand]
        private void UncheckAllReports()
        {
            foreach (var report in DownloadedReports)
            {
                report.IsSelected = false;
            }
            StatusMessage = "Unselected all reports";
        }

        [RelayCommand(CanExecute = nameof(HasSelectedReports))]
        private async Task PrintSelectedReportsAsync()
        {
            try
            {
                var selectedReports = DownloadedReports.Where(r => r.IsSelected).ToList();
                if (selectedReports.Count == 0)
                    return;

                StatusMessage = $"Printing {selectedReports.Count} report(s)...";

                var filePaths = selectedReports.Select(r => r.FilePath).ToList();
                int printedCount = await _printService.PrintMultiplePdfsAsync(filePaths, showDialog: true);

                if (printedCount > 0)
                {
                    StatusMessage = $"Successfully sent {printedCount} report(s) to printer";
                }
                else
                {
                    StatusMessage = "Print cancelled";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error printing reports: {ex.Message}";
                _logger.LogError(ex, "Error printing selected reports");
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedReports))]
        private async Task MoveSelectedReportsAsync()
        {
            try
            {
                var selectedReports = DownloadedReports.Where(r => r.IsSelected).ToList();
                if (selectedReports.Count == 0)
                    return;

                // Show folder browser dialog
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select destination folder for reports",
                    InitialDirectory = _configuration["Storage:AlteredFolder"] ?? @"C:\RemittanceAdvice\Altered"
                };

                if (dialog.ShowDialog() != true)
                {
                    StatusMessage = "Move cancelled";
                    return;
                }

                var destinationFolder = dialog.FolderName;
                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                StatusMessage = $"Moving {selectedReports.Count} report(s) to {destinationFolder}...";
                int movedCount = 0;
                var reportsToRemove = new System.Collections.Generic.List<DownloadedReport>();

                foreach (var report in selectedReports)
                {
                    try
                    {
                        var destPath = Path.Combine(destinationFolder, report.FileName);
                        File.Move(report.FilePath, destPath, overwrite: true);

                        reportsToRemove.Add(report);
                        movedCount++;
                        _logger.LogInformation($"Moved {report.FileName} to {destinationFolder}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error moving {report.FileName}");
                    }
                }

                // Remove all successfully moved reports from the collection in one pass
                foreach (var report in reportsToRemove)
                {
                    DownloadedReports.Remove(report);
                }

                StatusMessage = $"Moved {movedCount} of {selectedReports.Count} report(s) to {destinationFolder}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error moving reports: {ex.Message}";
                _logger.LogError(ex, "Error moving selected reports");
            }
        }

        private bool HasSelectedReports() => DownloadedReports.Any(r => r.IsSelected);

        [RelayCommand(CanExecute = nameof(HasSelectedReports))]
        private void DeleteSelectedReports()
        {
            try
            {
                var selectedReports = DownloadedReports.Where(r => r.IsSelected).ToList();
                if (selectedReports.Count == 0)
                    return;

                // Ask for confirmation
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete {selectedReports.Count} report(s)?\n\nThis action cannot be undone.",
                    "Confirm Delete",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
                {
                    StatusMessage = "Delete cancelled";
                    return;
                }

                StatusMessage = $"Deleting {selectedReports.Count} report(s)...";
                int deletedCount = 0;

                foreach (var report in selectedReports.ToList())
                {
                    try
                    {
                        if (File.Exists(report.FilePath))
                        {
                            File.Delete(report.FilePath);
                            deletedCount++;
                            _logger.LogInformation($"Deleted {report.FileName}");
                        }

                        DownloadedReports.Remove(report);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error deleting {report.FileName}");
                    }
                }

                StatusMessage = $"Deleted {deletedCount} of {selectedReports.Count} report(s)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting reports: {ex.Message}";
                _logger.LogError(ex, "Error deleting selected reports");
            }
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select download folder for reports",
                InitialDirectory = DownloadFolder
            };

            if (dialog.ShowDialog() == true)
            {
                DownloadFolder = dialog.FolderName;
                StatusMessage = $"Download folder set to: {DownloadFolder}";
                _logger.LogInformation($"Download folder changed to: {DownloadFolder}");

                // Load existing PDFs from the new folder
                LoadExistingReports();
            }
        }

        [RelayCommand]
        private void LoadExistingReports()
        {
            try
            {
                if (!Directory.Exists(DownloadFolder))
                {
                    StatusMessage = "Download folder does not exist";
                    return;
                }

                DownloadedReports.Clear();

                var pdfFiles = Directory.GetFiles(DownloadFolder, "*.pdf");

                foreach (var filePath in pdfFiles)
                {
                    var fileInfo = new FileInfo(filePath);
                    var fileName = fileInfo.Name;

                    // Try to parse provider number and advice date from filename
                    // Expected format: ProviderCheckTotals_MM_dd_yyyy_ProviderNumber.pdf
                    string providerNumber = "Unknown";
                    DateTime? adviceDate = null;

                    try
                    {
                        var parts = Path.GetFileNameWithoutExtension(fileName).Split('_');
                        if (parts.Length >= 5 && parts[0] == "ProviderCheckTotals")
                        {
                            // Parse date from MM_dd_yyyy
                            if (DateTime.TryParse($"{parts[1]}/{parts[2]}/{parts[3]}", out var parsedDate))
                            {
                                adviceDate = parsedDate;
                            }
                            // Last part is provider number
                            providerNumber = parts[4];
                        }
                    }
                    catch
                    {
                        // If parsing fails, just use the filename as-is
                    }

                    var report = new DownloadedReport
                    {
                        ProviderNumber = providerNumber,
                        AdviceDate = adviceDate ?? DateTime.MinValue,
                        FilePath = filePath,
                        FileName = fileName,
                        FileSize = fileInfo.Length,
                        DownloadedAt = fileInfo.LastWriteTime,
                        IsSelected = false
                    };

                    // Subscribe to property changes to update command states
                    report.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(DownloadedReport.IsSelected))
                        {
                            PrintSelectedReportsCommand.NotifyCanExecuteChanged();
                            MoveSelectedReportsCommand.NotifyCanExecuteChanged();
                            DeleteSelectedReportsCommand.NotifyCanExecuteChanged();
                        }
                    };

                    DownloadedReports.Add(report);
                }

                StatusMessage = $"Loaded {DownloadedReports.Count} report(s) from {DownloadFolder}";
                _logger.LogInformation($"Loaded {DownloadedReports.Count} existing reports from folder");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading existing reports: {ex.Message}";
                _logger.LogError(ex, "Error loading existing reports from folder");
            }
        }
    }
}
