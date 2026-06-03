using System.Windows.Controls;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

public partial class BriefingView : UserControl
{
    public BriefingView(BriefingViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
