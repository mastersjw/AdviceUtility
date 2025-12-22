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
    public partial class DownloadedFilesViewModel : ObservableObject
    {
        private readonly IPrintService _printService;
        private readonly IRemittanceDownloadService _downloadService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DownloadedFilesViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<RemittanceFile> _downloadedFiles = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private string _downloadProgress = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DownloadRemittanceAdvicesCommand))]
        private string _username = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DownloadRemittanceAdvicesCommand))]
        private string _password = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DownloadRemittanceAdvicesCommand))]
        private string _selectedDate = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _availableDates = new();

        public DownloadedFilesViewModel(
            IPrintService printService,
            IRemittanceDownloadService downloadService,
            IConfiguration configuration,
            ILogger<DownloadedFilesViewModel> logger)
        {
            _printService = printService;
            _downloadService = downloadService;
            _configuration = configuration;
            _logger = logger;

            // Load available dates (last 3 Mondays)
            var mondays = _downloadService.GetLastThreeMondays();
            AvailableDates = new ObservableCollection<string>(mondays);
            if (mondays.Count > 0)
                SelectedDate = mondays[0];

            // Load files on initialization
            _ = LoadFilesAsync();
        }

        [RelayCommand]
        private async Task LoadFilesAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading files...";

                var downloadFolder = _configuration["Storage:DownloadFolder"] ?? @"C:\RemittanceAdvice\Downloaded";

                if (!Directory.Exists(downloadFolder))
                {
                    Directory.CreateDirectory(downloadFolder);
                }

                var files = Directory.GetFiles(downloadFolder, "*.pdf")
                    .Select(filePath =>
                    {
                        var fileInfo = new FileInfo(filePath);
                        var file = new RemittanceFile
                        {
                            FileName = fileInfo.Name,
                            FilePath = filePath,
                            FileSize = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTime
                        };

                        // Subscribe to property changes to update command states
                        file.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(RemittanceFile.IsSelected))
                            {
                                PrintSelectedCommand.NotifyCanExecuteChanged();
                                DeleteSelectedCommand.NotifyCanExecuteChanged();
                            }
                        };

                        return file;
                    })
                    .OrderByDescending(f => f.LastModified)
                    .ToList();

                DownloadedFiles = new ObservableCollection<RemittanceFile>(files);

                StatusMessage = $"Loaded {files.Count} files";
                _logger.LogInformation($"Loaded {files.Count} downloaded files");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading files: {ex.Message}";
                _logger.LogError(ex, "Error loading downloaded files");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanPrintSelected))]
        private async Task PrintSelectedAsync()
        {
            try
            {
                var selectedFiles = DownloadedFiles.Where(f => f.IsSelected).ToList();

                if (selectedFiles.Count == 0)
                {
                    StatusMessage = "No files selected";
                    return;
                }

                StatusMessage = $"Printing {selectedFiles.Count} file(s)...";

                var filePaths = selectedFiles.Select(f => f.FilePath).ToList();
                int printedCount = await _printService.PrintMultiplePdfsAsync(filePaths, showDialog: true);

                if (printedCount > 0)
                {
                    StatusMessage = $"Successfully sent {printedCount} file(s) to printer";
                    _logger.LogInformation($"Printed {printedCount} files");
                }
                else
                {
                    StatusMessage = "Print cancelled";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error printing files: {ex.Message}";
                _logger.LogError(ex, "Error printing selected files");
            }
        }

        private bool CanPrintSelected() => DownloadedFiles.Any(f => f.IsSelected);

        [RelayCommand]
        private async Task PrintAllAsync()
        {
            try
            {
                var paths = DownloadedFiles.Select(f => f.FilePath).ToList();

                if (paths.Count == 0)
                {
                    StatusMessage = "No files to print";
                    return;
                }

                StatusMessage = $"Printing {paths.Count} file(s)...";

                int printedCount = await _printService.PrintMultiplePdfsAsync(paths, showDialog: true);

                if (printedCount > 0)
                {
                    StatusMessage = $"Successfully sent {printedCount} file(s) to printer";
                    _logger.LogInformation($"Printed {printedCount} files");
                }
                else
                {
                    StatusMessage = "Print cancelled";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error printing files: {ex.Message}";
                _logger.LogError(ex, "Error printing all files");
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
        private async Task DeleteSelectedAsync()
        {
            try
            {
                var selectedFiles = DownloadedFiles.Where(f => f.IsSelected).ToList();

                if (selectedFiles.Count == 0)
                {
                    StatusMessage = "No files selected";
                    return;
                }

                foreach (var file in selectedFiles)
                {
                    if (File.Exists(file.FilePath))
                    {
                        File.Delete(file.FilePath);
                    }
                    DownloadedFiles.Remove(file);
                }

                StatusMessage = $"Deleted {selectedFiles.Count} file(s)";
                _logger.LogInformation($"Deleted {selectedFiles.Count} files");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting files: {ex.Message}";
                _logger.LogError(ex, "Error deleting files");
            }
        }

        private bool CanDeleteSelected() => DownloadedFiles.Any(f => f.IsSelected);

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var file in DownloadedFiles)
            {
                file.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAll()
        {
            foreach (var file in DownloadedFiles)
            {
                file.IsSelected = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanDownload))]
        private async Task DownloadRemittanceAdvicesAsync()
        {
            try
            {
                IsDownloading = true;
                DownloadProgress = "Starting download...";

                // Get download path from configuration
                string downloadPath = _configuration["Storage:DownloadFolder"] ?? @"C:\RemittanceAdvice\Downloaded";
                string alteredPath = _configuration["Storage:AlteredFolder"] ?? @"C:\RemittanceAdvice\Altered";

                // Ensure directories exist
                if (!Directory.Exists(downloadPath))
                {
                    Directory.CreateDirectory(downloadPath);
                }

                // Download files
                var downloadedFiles = await _downloadService.DownloadRemittanceAdvicesAsync(
                    Username,
                    Password,
                    SelectedDate,
                    downloadPath,
                    progress => DownloadProgress = progress);

                // Reload files to show newly downloaded ones
                await LoadFilesAsync();

                DownloadProgress = $"Complete! Downloaded {downloadedFiles.Count} files.";
                _logger.LogInformation($"Downloaded {downloadedFiles.Count} remittance advice files");
            }
            catch (Exception ex)
            {
                DownloadProgress = $"Error: {ex.Message}";
                _logger.LogError(ex, "Error downloading remittance advices");
            }
            finally
            {
                IsDownloading = false;
            }
        }

        private bool CanDownload() =>
            !IsDownloading &&
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(Password) &&
            !string.IsNullOrWhiteSpace(SelectedDate);
    }
}
