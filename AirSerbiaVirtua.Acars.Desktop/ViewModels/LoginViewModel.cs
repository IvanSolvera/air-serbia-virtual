using AirSerbiaVirtua.Acars.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly ISessionService _session;

    [ObservableProperty] private string _callsign = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    public LoginViewModel(ISessionService session) => _session = session;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var result = await _session.LoginAsync(Callsign.Trim(), Password);
            if (!result.Success) ErrorMessage = result.ErrorMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLogin() =>
        !IsBusy && !string.IsNullOrWhiteSpace(Callsign) && !string.IsNullOrEmpty(Password);

    partial void OnCallsignChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnPasswordChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => LoginCommand.NotifyCanExecuteChanged();
}
