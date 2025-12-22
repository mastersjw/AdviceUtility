using CommunityToolkit.Mvvm.ComponentModel;

namespace RemittanceAdviceManager.Models
{
    public partial class ProviderNumber : ObservableObject
    {
        [ObservableProperty]
        private string _number = string.Empty;

        [ObservableProperty]
        private bool _isSelected;
    }
}
