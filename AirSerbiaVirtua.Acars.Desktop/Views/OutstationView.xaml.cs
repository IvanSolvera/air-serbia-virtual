using System.Windows.Controls;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

public partial class OutstationView : UserControl
{
    public OutstationView(OutstationViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
