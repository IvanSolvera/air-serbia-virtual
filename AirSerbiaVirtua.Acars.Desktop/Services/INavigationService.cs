using System.ComponentModel;

namespace AirSerbiaVirtua.Acars.Desktop.Services;

public enum NavTarget
{
    Login,
    PilotCentre,
    Bookings,
    Briefing,
    Acars,
    Debriefing,
    Logbook
}

/// <summary>Swaps the active view in the shell's content area.</summary>
public interface INavigationService : INotifyPropertyChanged
{
    NavTarget Current { get; }
    object? CurrentView { get; }

    void NavigateTo(NavTarget target);
}
