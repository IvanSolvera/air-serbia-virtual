using System.Windows.Controls;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

public partial class PilotCentreView : UserControl
{
    public PilotCentreView(PilotCentreViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    // PasswordBox content can't be data-bound (security); push into the VM on change.
    private void CurrentPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is PilotCentreViewModel vm && sender is PasswordBox pb) vm.CurrentPassword = pb.Password;
    }

    private void NewPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is PilotCentreViewModel vm && sender is PasswordBox pb) vm.NewPassword = pb.Password;
    }

    private void ConfirmPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is PilotCentreViewModel vm && sender is PasswordBox pb) vm.ConfirmPassword = pb.Password;
    }
}
