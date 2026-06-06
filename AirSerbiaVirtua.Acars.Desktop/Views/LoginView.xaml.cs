using System.Windows;
using System.Windows.Controls;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;
using MahApps.Metro.IconPacks;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

public partial class LoginView : UserControl
{
    private bool _revealed;

    public LoginView(LoginViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    // PasswordBox content can't be data-bound directly (security). Push the
    // value into the VM on each change.
    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is PasswordBox pb)
            vm.Password = pb.Password;
    }

    private void PasswordPlain_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is TextBox tb)
            vm.Password = tb.Text;
    }

    // Eye toggle: swap the masked PasswordBox for a plain TextBox showing the
    // same value, and back. Both push into the VM, so login always works.
    private void Reveal_OnClick(object sender, RoutedEventArgs e)
    {
        _revealed = !_revealed;
        if (_revealed)
        {
            PasswordPlain.Text = PasswordBox.Password;
            PasswordPlain.Visibility = Visibility.Visible;
            PasswordBox.Visibility = Visibility.Collapsed;
            RevealIcon.Kind = PackIconMaterialKind.EyeOffOutline;
            PasswordPlain.Focus();
            PasswordPlain.CaretIndex = PasswordPlain.Text.Length;
        }
        else
        {
            PasswordBox.Password = PasswordPlain.Text;
            PasswordBox.Visibility = Visibility.Visible;
            PasswordPlain.Visibility = Visibility.Collapsed;
            RevealIcon.Kind = PackIconMaterialKind.EyeOutline;
            PasswordBox.Focus();
        }
    }
}
