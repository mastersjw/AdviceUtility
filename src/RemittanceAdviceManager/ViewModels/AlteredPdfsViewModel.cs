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
    public partial class AlteredPdfsViewModel : ObservableObject
    {
        private readonly IPrintService _printService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AlteredPdfsViewModel> _logger;

        [ObservableProperty]
        private ObservableCollection<RemittanceFile> _alteredFiles = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public AlteredPdfsViewModel(
            IPrintService printService,
            IConfiguration configuration,
            ILogger<AlteredPdfsViewModel> logger)
        {
            _printService = printService;
            _configuration = configuration;
            _logger = logger;

            _ = LoadFilesAsync();
        }

        [RelayCommand]
        private async Task LoadFilesAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading altered files...";

                var alteredFolder = _configuration["Storage:AlteredFolder"] ?? @"C:\RemittanceAdvice\Altered";

                if (!Directory.Exists(alteredFolder))
                {
                    Directory.CreateDirectory(alteredFolder);
                }

                var files = Directory.GetFiles(alteredFolder, "*.pdf")
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

                AlteredFiles = new ObservableCollection<RemittanceFile>(files);

                StatusMessage = $"Loaded {files.Count} altered files";
                _logger.LogInformation($"Loaded {files.Count} altered files");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading files: {ex.Message}";
                _logger.LogError(ex, "Error loading altered files");
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
                var selectedFiles = AlteredFiles.Where(f => f.IsSelected).ToList();

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

        private bool CanPrintSelected() => AlteredFiles.Any(f => f.IsSelected);

        [RelayCommand]
        private async Task PrintAllAsync()
        {
            try
            {
                var paths = AlteredFiles.Select(f => f.FilePath).ToList();

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
                    _logger.LogInformation($"Printed {printedCount} altered files");
                }
                else
                {
                    StatusMessage = "Print cancelled";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error printing files: {ex.Message}";
                _logger.LogError(ex, "Error printing all altered files");
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
        private async Task DeleteSelectedAsync()
        {
            try
            {
                var selectedFiles = AlteredFiles.Where(f => f.IsSelected).ToList();

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
                    AlteredFiles.Remove(file);
                }

                StatusMessage = $"Deleted {selectedFiles.Count} file(s)";
                _logger.LogInformation($"Deleted {selectedFiles.Count} altered files");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting files: {ex.Message}";
                _logger.LogError(ex, "Error deleting altered files");
            }
        }

        private bool CanDeleteSelected() => AlteredFiles.Any(f => f.IsSelected);

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var file in AlteredFiles)
            {
                file.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAll()
        {
            foreach (var file in AlteredFiles)
            {
                file.IsSelected = false;
            }
        }
    }
}
