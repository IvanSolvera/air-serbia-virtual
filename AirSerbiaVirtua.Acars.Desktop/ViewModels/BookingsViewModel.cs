using System.Collections.ObjectModel;
using System.Windows;
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using AirSerbiaVirtua.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

public sealed partial class BookingsViewModel : ObservableObject
{
    private readonly ISessionService _session;
    private readonly FlightSessionState _flightState;
    private readonly DispatchService _dispatch;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _hasError;

    public ObservableCollection<RouteRow> Routes { get; } = new();
    public ObservableCollection<BookingRow> MyBookings { get; } = new();

    public BookingsViewModel(
        ISessionService session,
        FlightSessionState flightState,
        DispatchService dispatch)
    {
        _session = session;
        _flightState = flightState;
        _dispatch = dispatch;
        _session.StateChanged += (_, _) =>
            Application.Current?.Dispatcher.Invoke(() => RefreshCommand.NotifyCanExecuteChanged());
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    public async Task RefreshAsync()
    {
        if (!_session.IsAuthenticated)
        {
            StatusMessage = "Sign in to view available routes.";
            HasError = false;
            Routes.Clear();
            MyBookings.Clear();
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        HasError = false;
        try
        {
            var hub = _session.Pilot?.HubId;
            var routeTask = _session.Api.GetRoutesAsync(hub);
            var bookingsTask = _session.Api.GetMyBookingsAsync();
            await Task.WhenAll(routeTask, bookingsTask);

            Routes.Clear();
            foreach (var r in routeTask.Result)
                Routes.Add(new RouteRow(r));

            // Hide Cancelled/Expired/Flown from the active list — they belong in the Logbook.
            MyBookings.Clear();
            foreach (var b in bookingsTask.Result.Where(IsActive))
                MyBookings.Add(new BookingRow(b));

            if (Routes.Count == 0)
                StatusMessage = "No routes match this hub.";
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            BookRouteCommand.NotifyCanExecuteChanged();
            CancelBookingCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanBook))]
    private async Task BookRouteAsync(RouteRow? row)
    {
        if (row is null || !_session.IsAuthenticated) return;

        IsBusy = true;
        StatusMessage = null;
        HasError = false;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var booked = await _session.Api.CreateBookingAsync(row.Id, today);
            MyBookings.Insert(0, new BookingRow(booked));
            StatusMessage = $"Booked {booked.FlightNumber} on {booked.Date:yyyy-MM-dd}.";
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartFlight))]
    private async Task StartFlightAsync(BookingRow? row)
    {
        if (row is null || !_session.IsAuthenticated) return;

        IsBusy = true;
        StatusMessage = null;
        HasError = false;
        try
        {
            var result = await _dispatch.StartAsync(row.Source);
            if (!result.Success)
            {
                HasError = true;
                StatusMessage = result.Message;
                return;
            }

            row.Status = BookingStatus.Confirmed;
            StatusMessage = $"Flight {row.FlightNumber} started on {result.Registration}. Switching to ACARS Live.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelBookingAsync(BookingRow? row)
    {
        if (row is null || !_session.IsAuthenticated) return;

        IsBusy = true;
        StatusMessage = null;
        HasError = false;
        try
        {
            await _session.Api.CancelBookingAsync(row.Id);
            MyBookings.Remove(row);
            StatusMessage = $"Cancelled {row.FlightNumber} ({row.Date:yyyy-MM-dd}).";
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRefresh() => !IsBusy;

    private static bool IsActive(BookingInfo b) =>
        b.Status is BookingStatus.Open or BookingStatus.Confirmed;
    private bool CanBook(RouteRow? row) => row is not null && !IsBusy && _session.IsAuthenticated;
    private bool CanCancel(BookingRow? row) =>
        row is not null && !IsBusy && _session.IsAuthenticated &&
        row.Status is BookingStatus.Open or BookingStatus.Confirmed;
    private bool CanStartFlight(BookingRow? row) =>
        row is not null && !IsBusy && _session.IsAuthenticated &&
        row.Status is BookingStatus.Open or BookingStatus.Confirmed &&
        !_flightState.HasActiveSession;
}

public sealed class RouteRow
{
    public int Id { get; }
    public string FlightNumber { get; }
    public string DepIcao { get; }
    public string ArrIcao { get; }
    public string LegLabel => $"{DepIcao} → {ArrIcao}";
    public string AircraftType { get; }
    public int DistanceNm { get; }
    public int PlannedMinutes { get; }
    public string PlannedTimeLabel => $"{PlannedMinutes / 60}h {PlannedMinutes % 60:D2}m";

    public RouteRow(RouteInfo r)
    {
        Id = r.Id;
        FlightNumber = r.FlightNumber;
        DepIcao = r.DepIcao;
        ArrIcao = r.ArrIcao;
        AircraftType = r.AircraftType;
        DistanceNm = r.DistanceNm;
        PlannedMinutes = r.PlannedMinutes;
    }
}

public sealed partial class BookingRow : ObservableObject
{
    public int Id { get; }
    public int RouteId { get; }
    public string FlightNumber { get; }
    public string DepIcao { get; }
    public string ArrIcao { get; }
    public string LegLabel => $"{DepIcao} → {ArrIcao}";
    public string AircraftType { get; }
    public DateOnly Date { get; }

    /// <summary>The wire DTO this row was built from (used by DispatchService + the READY chip).</summary>
    public BookingInfo Source { get; }

    [ObservableProperty] private BookingStatus _status;
    public string StatusLabel => Status.ToString();

    public BookingRow(BookingInfo b)
    {
        Source = b;
        Id = b.Id;
        RouteId = b.RouteId;
        FlightNumber = b.FlightNumber;
        DepIcao = b.DepIcao;
        ArrIcao = b.ArrIcao;
        AircraftType = b.AircraftType;
        Date = b.Date;
        _status = b.Status;
    }

    partial void OnStatusChanged(BookingStatus value) => OnPropertyChanged(nameof(StatusLabel));
}
