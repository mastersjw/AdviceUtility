using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using RemittanceAdviceManager.ViewModels;

namespace RemittanceAdviceManager.Views
{
    public partial class DownloadReportsView : System.Windows.Controls.UserControl
    {
        public DownloadReportsView()
        {
            InitializeComponent();

            // Handle password changes since PasswordBox doesn't support binding
            PasswordBox.PasswordChanged += (s, e) =>
            {
                if (DataContext is DownloadReportsViewModel viewModel)
                {
                    viewModel.Credentials.Password = PasswordBox.Password;
                }
            };
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
