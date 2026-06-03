using System.Windows.Controls;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

public partial class AcarsView : UserControl
{
    public AcarsView(AcarsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
