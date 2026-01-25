using System.Security.Cryptography;
using System.Text;
using System.Windows;
using TamponiInventario.Wpf.Config;

namespace TamponiInventario.Wpf.Services;

public sealed class AdminSession
{
    private static readonly Lazy<AdminSession> LazyInstance = new(() => new AdminSession());

    public static AdminSession Instance => LazyInstance.Value;

    private readonly string _adminPassword;

    public bool IsAuthenticated { get; private set; }

    private AdminSession()
    {
        _adminPassword = LocalConfig.Load().AdminPassword;
    }

    public bool EnsureAuthenticated(Window? owner)
    {
        if (IsAuthenticated)
        {
            return true;
        }

        var dialog = new Views.LoginDialog
        {
            Owner = owner ?? Application.Current?.MainWindow
        };

        return dialog.ShowDialog() == true;
    }

    public bool TryLogin(string password)
    {
        var ok = PasswordMatches(password);
        if (ok)
        {
            IsAuthenticated = true;
        }

        return ok;
    }

    private bool PasswordMatches(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(_adminPassword);
        var providedBytes = Encoding.UTF8.GetBytes(password);
        if (expectedBytes.Length != providedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
