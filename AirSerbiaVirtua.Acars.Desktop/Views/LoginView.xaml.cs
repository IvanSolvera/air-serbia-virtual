using System.Windows.Controls;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

public partial class LoginView : UserControl
{
    public LoginView(LoginViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    // PasswordBox content can't be data-bound directly (security). Push the
    // value into the VM on each change.
    private void PasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is PasswordBox pb)
            vm.Password = pb.Password;
    }
}
