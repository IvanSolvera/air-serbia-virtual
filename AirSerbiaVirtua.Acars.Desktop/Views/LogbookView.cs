using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

/// <summary>Phase-1 placeholder. Will list submitted PIREPs (filterable, exportable) in Phase 4.</summary>
public sealed class LogbookView : PlaceholderView
{
    public LogbookView() : base(new PlaceholderViewModel
    {
        Title = "Logbook",
        Description = "Coming in Phase 4: full PIREP history with filters, landing rates, scores and totals."
    }) { }
}
