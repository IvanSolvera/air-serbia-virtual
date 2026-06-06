using System.Windows.Controls;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

public partial class AdminView : UserControl
{
    public AdminView(AdminViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
