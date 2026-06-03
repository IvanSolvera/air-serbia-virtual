using System.Windows.Controls;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

public partial class DebriefingView : UserControl
{
    public DebriefingView(DebriefingViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
