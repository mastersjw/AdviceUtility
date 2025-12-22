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
    public partial class UploadToWebDbViewModel : ObservableObject
    {
        private readonly IWebDbAuthenticationService _authService;
        private readonly IRemittanceUploadService _uploadService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UploadToWebDbViewModel> _logger;

        [ObservableProperty]
        private WebDbCredentials _credentials = new();

        [ObservableProperty]
        private bool _isAuthenticated;

        [ObservableProperty]
        private ObservableCollection<RemittanceFile> _filesToUpload = new();

        [ObservableProperty]
        private int _uploadProgress;

        [ObservableProperty]
        private string _uploadStatus = string.Empty;

        [ObservableProperty]
        private bool _isUploading;

        [ObservableProperty]
        private bool _overwriteExisting;

        [ObservableProperty]
        private ObservableCollection<UploadResult> _uploadResults = new();

        public UploadToWebDbViewModel(
            IWebDbAuthenticationService authService,
            IRemittanceUploadService uploadService,
            IConfiguration configuration,
            ILogger<UploadToWebDbViewModel> logger)
        {
            _authService = authService;
            _uploadService = uploadService;
            _configuration = configuration;
            _logger = logger;

            // Subscribe to authentication state changes from the service
            _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        private void OnAuthenticationStateChanged(object? sender, bool isAuthenticated)
        {
            IsAuthenticated = isAuthenticated;
            if (isAuthenticated)
            {
                UploadStatus = "Authenticated successfully";
                _ = LoadFilesToUploadAsync();
            }
            else
            {
                UploadStatus = "Logged out";
            }
        }

        [RelayCommand]
        private async Task AuthenticateAsync()
        {
            try
            {
                UploadStatus = "Authenticating...";
                IsAuthenticated = await _authService.AuthenticateAsync(Credentials);

                if (IsAuthenticated)
                {
                    UploadStatus = $"Successfully authenticated as {Credentials.Username}";
                    _logger.LogInformation($"Successfully authenticated as {Credentials.Username}");

                    await LoadFilesToUploadAsync();
                }
                else
                {
                    UploadStatus = "Authentication failed. Please check your credentials.";
                    _logger.LogWarning("Authentication failed");
                }
            }
            catch (Exception ex)
            {
                UploadStatus = $"Authentication error: {ex.Message}";
                _logger.LogError(ex, "Authentication error");
            }
        }

        [RelayCommand]
        private async Task LoadFilesToUploadAsync()
        {
            try
            {
                var downloadFolder = _configuration["Storage:DownloadFolder"] ?? @"C:\RemittanceAdvice\Downloaded";

                if (!Directory.Exists(downloadFolder))
                {
                    Directory.CreateDirectory(downloadFolder);
                }

                var files = Directory.GetFiles(downloadFolder, "*.pdf")
                    .Select(filePath =>
                    {
                        var fileInfo = new FileInfo(filePath);
                        return new RemittanceFile
                        {
                            FileName = fileInfo.Name,
                            FilePath = filePath,
                            FileSize = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTime
                        };
                    })
                    .OrderByDescending(f => f.LastModified)
                    .ToList();

                FilesToUpload = new ObservableCollection<RemittanceFile>(files);

                UploadStatus = $"Loaded {files.Count} files ready to upload";
                _logger.LogInformation($"Loaded {files.Count} files for upload");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                UploadStatus = $"Error loading files: {ex.Message}";
                _logger.LogError(ex, "Error loading files for upload");
            }
        }

        [RelayCommand(CanExecute = nameof(CanUpload))]
        private async Task UploadSelectedFilesAsync()
        {
            try
            {
                IsUploading = true;
                UploadProgress = 0;
                UploadResults.Clear();

                var selectedFiles = FilesToUpload.Where(f => f.IsSelected).ToList();

                if (selectedFiles.Count == 0)
                {
                    UploadStatus = "No files selected for upload";
                    return;
                }

                UploadStatus = $"Uploading {selectedFiles.Count} files...";

                var progress = new Progress<int>(value => UploadProgress = value);

                var results = await _uploadService.UploadMultipleAsync(
                    selectedFiles,
                    OverwriteExisting,
                    progress);

                // Update UI with results
                foreach (var result in results)
                {
                    UploadResults.Add(result);
                }

                var successCount = results.Count(r => r.Success);
                UploadStatus = $"Completed: {successCount} of {results.Count} files uploaded successfully";
                _logger.LogInformation($"Upload completed: {successCount}/{results.Count} successful");
            }
            catch (Exception ex)
            {
                UploadStatus = $"Upload error: {ex.Message}";
                _logger.LogError(ex, "Error during upload");
            }
            finally
            {
                IsUploading = false;
            }
        }

        private bool CanUpload() => IsAuthenticated && FilesToUpload.Any(f => f.IsSelected) && !IsUploading;

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var file in FilesToUpload)
            {
                file.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAll()
        {
            foreach (var file in FilesToUpload)
            {
                file.IsSelected = false;
            }
        }

        [RelayCommand]
        private async Task LogoutAsync()
        {
            await _authService.LogoutAsync();
            IsAuthenticated = false;
            UploadStatus = "Logged out";
            FilesToUpload.Clear();
            _logger.LogInformation("User logged out");
        }
    }
}
