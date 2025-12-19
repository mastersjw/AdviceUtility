using System.Windows.Controls;
using RemittanceAdviceManager.ViewModels;

namespace RemittanceAdviceManager.Views
{
    public partial class DownloadedFilesView : UserControl
    {
        public DownloadedFilesView()
        {
            InitializeComponent();

            // Handle password changes since PasswordBox doesn't support binding
            PasswordBox.PasswordChanged += (s, e) =>
            {
                if (DataContext is DownloadedFilesViewModel viewModel)
                {
                    viewModel.Password = PasswordBox.Password;
                }
            };
        }
    }
}
