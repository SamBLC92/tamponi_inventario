using System.Windows;
using TamponiInventario.Wpf.Services;

namespace TamponiInventario.Wpf.Views;

public partial class LoginDialog : Window
{
    public LoginDialog()
    {
        InitializeComponent();
        PasswordBox.Focus();
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;
        if (AdminSession.Instance.TryLogin(password))
        {
            DialogResult = true;
            Close();
            return;
        }

        ErrorText.Visibility = Visibility.Visible;
        PasswordBox.SelectAll();
        PasswordBox.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
