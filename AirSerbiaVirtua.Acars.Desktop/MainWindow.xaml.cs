using System.Windows;

namespace AirSerbiaVirtua.Acars.Desktop;

/// <summary>
/// Borderless "Flight Crew Suite" shell window. The chrome (titlebar drag area,
/// resize borders) is provided by <see cref="System.Windows.Shell.WindowChrome"/>;
/// the caption buttons are ours.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
