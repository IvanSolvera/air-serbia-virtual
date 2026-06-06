using System.Windows.Controls;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

public partial class MetarsView : UserControl
{
    public MetarsView(MetarsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
