using AirSerbiaVirtua.Acars.Desktop.ViewModels;

namespace AirSerbiaVirtua.Acars.Desktop.Views;

/// <summary>Phase-1 placeholder. Will host the route catalog + booking flow in Phase 3.</summary>
public sealed class BookingsView : PlaceholderView
{
    public BookingsView() : base(new PlaceholderViewModel
    {
        Title = "Bookings",
        Description = "Coming in Phase 3: browse routes from your hub, book a flight, view active bookings."
    }) { }
}
