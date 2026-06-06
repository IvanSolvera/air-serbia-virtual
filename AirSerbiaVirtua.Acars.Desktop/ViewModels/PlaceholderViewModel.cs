using MahApps.Metro.IconPacks;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

/// <summary>
/// Model for the "coming in Phase 5" placeholder pages (METARs, Outstation
/// Flights). The icon is the page's own nav icon, shown in the empty-state ring.
/// </summary>
public sealed class PlaceholderViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PackIconMaterialKind Icon { get; init; } = PackIconMaterialKind.Wrench;
}
