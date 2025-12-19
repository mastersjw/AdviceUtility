using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace RemittanceAdviceManager.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private int _selectedTabIndex = 0;

        public MainWindowViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            // Load initial view
            NavigateToDownloadedFiles();
        }

        [RelayCommand]
        private void NavigateToDownloadedFiles()
        {
            CurrentView = _serviceProvider.GetRequiredService<DownloadedFilesViewModel>();
            StatusMessage = "Downloaded Files";
        }

        [RelayCommand]
        private void NavigateToAlteredPdfs()
        {
            CurrentView = _serviceProvider.GetRequiredService<AlteredPdfsViewModel>();
            StatusMessage = "Altered PDFs";
        }

        [RelayCommand]
        private void NavigateToUploadToWebDb()
        {
            CurrentView = _serviceProvider.GetRequiredService<UploadToWebDbViewModel>();
            StatusMessage = "Upload to WebDB";
        }

        [RelayCommand]
        private void NavigateToDownloadReports()
        {
            CurrentView = _serviceProvider.GetRequiredService<DownloadReportsViewModel>();
            StatusMessage = "Download Reports";
        }
    }
}
