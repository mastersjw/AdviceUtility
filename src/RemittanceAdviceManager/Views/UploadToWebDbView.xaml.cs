using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using RemittanceAdviceManager.ViewModels;

namespace RemittanceAdviceManager.Views
{
    public partial class UploadToWebDbView : System.Windows.Controls.UserControl
    {
        public UploadToWebDbView()
        {
            InitializeComponent();

            // Handle password changes since PasswordBox doesn't support binding
            PasswordBox.PasswordChanged += (s, e) =>
            {
                if (DataContext is UploadToWebDbViewModel viewModel)
                {
                    viewModel.Credentials.Password = PasswordBox.Password;
                }
            };
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return value;
        }
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
