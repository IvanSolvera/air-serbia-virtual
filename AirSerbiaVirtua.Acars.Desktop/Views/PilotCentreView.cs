using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

/// <summary>Phase-1 placeholder. Will host pilot profile, hours, rank in Phase 2.</summary>
public sealed class PilotCentreView : PlaceholderView
{
    public PilotCentreView() : base(new PlaceholderViewModel
    {
        Title = "Pilot Centre",
        Description = "Coming in Phase 2: profile, hours, current rank, badges, hub assignment."
    }) { }
}
