using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TamponiInventario.Wpf.Services;

namespace TamponiInventario.Wpf.Views.Admin;

public partial class AdminView : UserControl
{
    public AdminView()
    {
        InitializeComponent();
    }

    private void AdminAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (!AdminSession.Instance.EnsureAuthenticated(Window.GetWindow(this)))
        {
            return;
        }

        var commandName = button.Tag as string;
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return;
        }

        var command = ResolveCommand(commandName);
        var parameter = button.CommandParameter;
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }

    private ICommand? ResolveCommand(string commandName)
    {
        if (DataContext is null)
        {
            return null;
        }

        var property = DataContext.GetType().GetProperty(commandName, BindingFlags.Instance | BindingFlags.Public);
        return property?.GetValue(DataContext) as ICommand;
    }
}
