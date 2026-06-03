using System.Windows.Controls;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

public partial class BookingsView : UserControl
{
    public BookingsView(BookingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) =>
        {
            if (vm.Routes.Count == 0 && !vm.IsBusy)
                await vm.RefreshAsync();
        };
    }
}
