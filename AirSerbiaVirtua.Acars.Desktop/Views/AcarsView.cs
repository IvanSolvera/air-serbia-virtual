using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

/// <summary>Phase-1 placeholder. Will wire FsuipcService + ApiService.PushPositionAsync in Phase 2.</summary>
public sealed class AcarsView : PlaceholderView
{
    public AcarsView() : base(new PlaceholderViewModel
    {
        Title = "ACARS Live",
        Description = "Coming in Phase 2: connect to MSFS via FSUIPC, track flight phases, push POSREP every 30s, submit PIREP on arrival."
    }) { }
}
