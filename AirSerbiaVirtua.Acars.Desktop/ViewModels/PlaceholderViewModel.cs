namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

/// <summary>
/// Shared model for the Phase-1 placeholder tabs (Pilot Centre, Bookings,
/// ACARS Live, Logbook). Each tab will get its own typed VM in the next phase.
/// </summary>
public sealed class PlaceholderViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
