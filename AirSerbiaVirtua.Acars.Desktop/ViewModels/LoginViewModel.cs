using AirSerbiaVirtua.Acars.Desktop.Services;
using AirSerbiaVirtua.Acars.Desktop.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly ISessionService _session;
    private readonly UiSettings _settings;

    [ObservableProperty] private string _callsign = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _rememberMe;
    [ObservableProperty] private bool _autoLogin;

    /// <summary>API host shown in the "Encrypted crew session" footer chip.</summary>
    public string ApiHostText { get; }

    public LoginViewModel(ISessionService session)
    {
        _session = session;

        _settings = UiSettings.Load();
        RememberMe = _settings.RememberMe;
        AutoLogin = _settings.AutoLogin;
        if (RememberMe && !string.IsNullOrWhiteSpace(_settings.SavedCallsign))
            Callsign = _settings.SavedCallsign;

        ApiHostText = _session.Api.BaseHost;
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            // Persist the toggles BEFORE the call: the token-persistence hook in
            // SessionService reads AutoLogin while the login response is processed.
            _settings.RememberMe = RememberMe;
            _settings.AutoLogin = AutoLogin;
            _settings.Save();

            var result = await _session.LoginAsync(Callsign.Trim(), Password);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            _settings.SavedCallsign = RememberMe ? Callsign.Trim() : null;
            _settings.Save();
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
