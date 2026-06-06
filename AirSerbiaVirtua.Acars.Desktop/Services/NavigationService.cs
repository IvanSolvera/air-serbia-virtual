using System.ComponentModel;
using System.Runtime.CompilerServices;
using AirSerbiaVirtua.Acars.Desktop.ViewModels;
using AirSerbiaVirtua.Acars.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AirSerbiaVirtua.Acars.Desktop.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _provider;
    private NavTarget _current = NavTarget.Login;
    private object? _currentView;

    public NavigationService(IServiceProvider provider)
    {
        _provider = provider;
        NavigateTo(NavTarget.Login);
    }

    public NavTarget Current
    {
        get => _current;
        private set { if (_current != value) { _current = value; OnPropertyChanged(); } }
    }

    public object? CurrentView
    {
        get => _currentView;
        private set { _currentView = value; OnPropertyChanged(); }
    }

    public void NavigateTo(NavTarget target)
    {
        CurrentView = target switch
        {
            NavTarget.Login => _provider.GetRequiredService<LoginView>(),
            NavTarget.PilotCentre => _provider.GetRequiredService<PilotCentreView>(),
            NavTarget.Bookings => _provider.GetRequiredService<BookingsView>(),
            NavTarget.Briefing => _provider.GetRequiredService<BriefingView>(),
            NavTarget.Acars => _provider.GetRequiredService<AcarsView>(),
            NavTarget.Debriefing => _provider.GetRequiredService<DebriefingView>(),
            NavTarget.Logbook => _provider.GetRequiredService<LogbookView>(),
            NavTarget.Metars => new PlaceholderView(new PlaceholderViewModel
            {
                Title = "METARs",
                Description = "Station weather lookup is coming in Phase 5. " +
                              "Live departure/arrival METARs are already on the Briefing page."
            }),
            NavTarget.Outstation => new PlaceholderView(new PlaceholderViewModel
            {
                Title = "Outstation Flights",
                Description = "Charter and one-off flights outside the scheduled network " +
                              "are coming in Phase 5."
            }),
            _ => null
        };
        Current = target;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
