using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace RemittanceAdviceManager.Models
{
    public partial class DownloadedReport : ObservableObject
    {
        [ObservableProperty]
        private string _providerNumber = string.Empty;

        [ObservableProperty]
        private DateTime _adviceDate;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private long _fileSize;

        [ObservableProperty]
        private DateTime _downloadedAt;

        [ObservableProperty]
        private bool _isSelected;
    }
}
