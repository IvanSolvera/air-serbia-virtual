using System.Windows.Controls;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

public partial class LogbookView : UserControl
{
    public LogbookView(LogbookViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
