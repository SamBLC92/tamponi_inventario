using System.Windows;
using System.Windows.Controls;
using TamponiInventario.Wpf.Data;
using TamponiInventario.Wpf.Services;

namespace TamponiInventario.Wpf.Views;

public partial class SwabsView : UserControl
{
    private readonly LabelService _labelService = new();

    public SwabsView()
    {
        InitializeComponent();
    }

    private void OpenLabel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SwabOverview swab })
        {
            _labelService.OpenLabel(swab.Sku);
        }
    }
}
