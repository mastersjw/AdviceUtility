using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RemittanceAdviceManager.Models;
using RemittanceAdviceManager.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace RemittanceAdviceManager.ViewModels
{
    public partial class DownloadedFilesViewModel : ObservableObject
    {
        private readonly IFileTrackingService _fileTrackingService;
        private readonly IPrintService _printService;
        private readonly ILogger<DownloadedFilesViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<RemittanceFile> _downloadedFiles = new();

        [ObservableProperty]
        private RemittanceFile? _selectedFile;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public DownloadedFilesViewModel(
            IFileTrackingService fileTrackingService,
            IPrintService printService,
            ILogger<DownloadedFilesViewModel> logger)
        {
            _fileTrackingService = fileTrackingService;
            _printService = printService;
            _logger = logger;

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

                var files = await _fileTrackingService.GetDownloadedFilesAsync();
                DownloadedFiles = new ObservableCollection<RemittanceFile>(files);

                StatusMessage = $"Loaded {files.Count} files";
                _logger.LogInformation($"Loaded {files.Count} downloaded files");
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
            if (SelectedFile == null) return;

            try
            {
                StatusMessage = $"Printing {SelectedFile.FileName}...";
                await _printService.PrintPdfAsync(SelectedFile.FilePath);

                await _fileTrackingService.MarkAsPrintedAsync(SelectedFile.Id);
                SelectedFile.IsPrinted = true;

                StatusMessage = $"Printed {SelectedFile.FileName}";
                _logger.LogInformation($"Printed file: {SelectedFile.FileName}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error printing file: {ex.Message}";
                _logger.LogError(ex, $"Error printing file: {SelectedFile.FileName}");
            }
        }

        private bool CanPrintSelected() => SelectedFile != null;

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

                StatusMessage = $"Printing {paths.Count} files...";
                await _printService.PrintMultiplePdfsAsync(paths);

                // Mark all as printed
                foreach (var file in DownloadedFiles)
                {
                    await _fileTrackingService.MarkAsPrintedAsync(file.Id);
                    file.IsPrinted = true;
                }

                StatusMessage = $"Printed {paths.Count} files";
                _logger.LogInformation($"Printed {paths.Count} files");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error printing files: {ex.Message}";
                _logger.LogError(ex, "Error printing all files");
            }
        }

        [RelayCommand(CanExecute = nameof(CanPrintSelected))]
        private async Task DeleteSelectedAsync()
        {
            if (SelectedFile == null) return;

            try
            {
                await _fileTrackingService.DeleteFileAsync(SelectedFile.Id);
                DownloadedFiles.Remove(SelectedFile);

                StatusMessage = $"Deleted {SelectedFile.FileName}";
                _logger.LogInformation($"Deleted file: {SelectedFile.FileName}");

                SelectedFile = null;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting file: {ex.Message}";
                _logger.LogError(ex, "Error deleting file");
            }
        }
    }
}
