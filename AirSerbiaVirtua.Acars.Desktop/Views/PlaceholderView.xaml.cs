using System.Windows.Controls;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

public partial class PlaceholderView : UserControl
{
    public PlaceholderView(PlaceholderViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
