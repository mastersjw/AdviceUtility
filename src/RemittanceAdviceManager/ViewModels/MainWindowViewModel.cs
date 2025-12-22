using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace RemittanceAdviceManager.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private int _selectedTabIndex = 0;

        public DownloadedFilesViewModel DownloadedFilesViewModel { get; }
        public AlteredPdfsViewModel AlteredPdfsViewModel { get; }
        public UploadToWebDbViewModel UploadToWebDbViewModel { get; }
        public DownloadReportsViewModel DownloadReportsViewModel { get; }

        public MainWindowViewModel(IServiceProvider serviceProvider)
        {
            DownloadedFilesViewModel = serviceProvider.GetRequiredService<DownloadedFilesViewModel>();
            AlteredPdfsViewModel = serviceProvider.GetRequiredService<AlteredPdfsViewModel>();
            UploadToWebDbViewModel = serviceProvider.GetRequiredService<UploadToWebDbViewModel>();
            DownloadReportsViewModel = serviceProvider.GetRequiredService<DownloadReportsViewModel>();
        }
    }
}
