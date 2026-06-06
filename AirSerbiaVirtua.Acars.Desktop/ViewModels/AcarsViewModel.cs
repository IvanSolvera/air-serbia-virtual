using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Windows;
using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirSerbiaVirtua.Acars.Desktop.ViewModels;

public sealed partial class AcarsViewModel : ObservableObject, IDisposable
{
    private readonly SimulatorService _sim;
    private readonly ISessionService _session;
    private readonly FlightSessionState _flightState;
    private readonly PosrepQueue _posrepQueue;
    private static readonly TimeSpan PosRepInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PosRepSendTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DrainBeforeSubmit = TimeSpan.FromSeconds(20);

    private CancellationTokenSource? _posrepCts;
    private Task? _posrepTask;
    private FlightState _currentPhase = FlightState.Preflight;

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _connectionButtonText = "Connect to sim";
    [ObservableProperty] private string _flightNumber = "—";
    [ObservableProperty] private string _tailNumber = "—";

    // Session header
    [ObservableProperty] private bool _hasActiveSession;
    [ObservableProperty] private string _sessionHeaderText = "No active flight session — open a booking and Start flight.";
    [ObservableProperty] private string _lastPosRepText = "—";
    [ObservableProperty] private string _queueStatusText = "Queue empty";
    [ObservableProperty] private string? _submitErrorMessage;
    [ObservableProperty] private bool _isSubmitting;

    // Telemetry tiles
    [ObservableProperty] private string _latText = "—";
    [ObservableProperty] private string _lonText = "—";
    [ObservableProperty] private string _altitudeText = "—";
    [ObservableProperty] private string _iasText = "—";
    [ObservableProperty] private string _gsText = "—";
    [ObservableProperty] private string _vsText = "—";
    [ObservableProperty] private string _fuelText = "—";
    [ObservableProperty] private string _groundStateText = "—";
    [ObservableProperty] private string _brakeStateText = "—";

    // Milestones & metrics
    [ObservableProperty] private string _offBlockText = "—";
    [ObservableProperty] private string _takeoffText = "—";
    [ObservableProperty] private string _landingText = "—";
    [ObservableProperty] private string _onBlockText = "—";
    [ObservableProperty] private string _blockTimeText = "—";
    [ObservableProperty] private string _airTimeText = "—";
    [ObservableProperty] private string _fuelUsedText = "—";
    [ObservableProperty] private string _landingRateText = "—";

    public ObservableCollection<PhaseItem> Phases { get; }

    public AcarsViewModel(
        SimulatorService sim,
        ISessionService session,
        FlightSessionState flightState)
    {
        _sim = sim;
        _session = session;
        _flightState = flightState;

        // Durable POSREP buffer: persists every sample to disk and delivers it
        // with retry/backoff, capping each send at 5 s so a stalled network never
        // blocks the next tick. Survives crashes and Wi-Fi blips (prod item #6).
        _posrepQueue = new PosrepQueue(
            send: async (env, ct) =>
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(PosRepSendTimeout);
                try
                {
                    await _session.Api.PushPositionAsync(env.FlightSessionId, env.Report, timeoutCts.Token)
                        .ConfigureAwait(false);
                }
                catch (ApiException ex) when (IsPermanent(ex.StatusCode))
                {
                    // Session closed / gone / malformed — never going to succeed; drop it.
                    throw new PosrepPermanentException(ex.Message);
                }
            });
        _posrepQueue.Changed += OnQueueChanged;
        _posrepQueue.Start();

        Phases = new ObservableCollection<PhaseItem>(
            Enum.GetValues<FlightState>().Select(s => new PhaseItem
            {
                State = s,
                Label = FormatPhase(s)
            }));
        UpdatePhaseHighlight(FlightState.Preflight);

        _sim.TelemetryUpdated += OnTelemetry;
        _sim.PhaseChanged += OnPhaseChanged;
        _sim.ConnectionStateChanged += OnConnectionChanged;
        _flightState.PropertyChanged += OnFlightStateChanged;
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (_sim.IsRunning)
        {
            await _sim.StopAsync();
            IsRunning = false;
            ConnectionButtonText = "Connect to sim";
        }
        else
        {
            _sim.Start();
            IsRunning = true;
            ConnectionButtonText = "Disconnect";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSubmitPirep))]
    private async Task SubmitPirepAsync()
    {
        if (_flightState.FlightSessionId is not { } sessionId) return;
        var sm = _sim.StateMachine;

        IsSubmitting = true;
        SubmitErrorMessage = null;
        try
        {
            var dep = sm.OffBlockUtc ?? _flightState.StartedAtUtc ?? DateTimeOffset.UtcNow;
            var arr = sm.OnBlockUtc ?? DateTimeOffset.UtcNow;
            var block = (int)(sm.BlockTime?.TotalMinutes ?? Math.Max(1, (arr - dep).TotalMinutes));
            var air = (int)(sm.AirTime?.TotalMinutes ?? 0);
            var fuel = (int)Math.Round(sm.FuelUsedKg ?? 0);
            var landing = (int)Math.Round(sm.LandingRateFpm);

            var req = new PirepSubmitRequest(
                FlightSessionId: sessionId,
                DepActual: dep,
                ArrActual: arr,
                BlockMin: block,
                AirMin: air,
                FuelUsedKg: fuel,
                LandingRateFpm: landing,
                Source: "ACARS",
                RawJson: null);

            // Flush any buffered POSREPs first so the recorded track is complete
            // before the session closes server-side.
            if (!await _posrepQueue.WaitForDrainAsync(DrainBeforeSubmit))
                SubmitErrorMessage = $"Warning: {_posrepQueue.PendingCount} POSREP(s) not yet delivered; submitting anyway.";

            await _session.Api.SubmitPirepAsync(req);

            _flightState.Clear();
            SessionHeaderText = $"PIREP submitted. Block {block}m, landing {landing} fpm.";
        }
        catch (Exception ex)
        {
            SubmitErrorMessage = ex.Message;
        }
        finally
        {
            IsSubmitting = false;
            SubmitPirepCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanSubmitPirep() =>
        !IsSubmitting && _flightState.HasActiveSession && _currentPhase == FlightState.Arrived;

    /// <summary>HTTP statuses a POSREP can never recover from — drop rather than retry.</summary>
    private static bool IsPermanent(HttpStatusCode code) => code is
        HttpStatusCode.BadRequest or       // 400 — malformed sample
        HttpStatusCode.NotFound or          // 404 — session id unknown
        HttpStatusCode.Conflict or          // 409 — session no longer active
        HttpStatusCode.Gone;                // 410

    // ============================================================ Telemetry
    private void OnTelemetry(FlightData data) =>
        OnUi(() =>
        {
            LatText  = $"{Math.Abs(data.LatitudeDeg):F4}° {(data.LatitudeDeg >= 0 ? "N" : "S")}";
            LonText  = $"{Math.Abs(data.LongitudeDeg):F4}° {(data.LongitudeDeg >= 0 ? "E" : "W")}";
            AltitudeText = $"{data.AltitudeFt:N0} ft";
            IasText = $"{data.IndicatedAirspeedKts:F0} kt";
            GsText  = $"{data.GroundSpeedKts:F0} kt";
            VsText  = $"{data.VerticalSpeedFpm:+0;-0;0} fpm";
            FuelText = $"{data.FuelKg:N0} kg";
            GroundStateText = data.OnGround ? "On ground" : "Airborne";
            BrakeStateText  = data.ParkingBrakeSet ? "Parking set" : "Brake released";
            FlightNumber = string.IsNullOrWhiteSpace(data.FlightNumber) ? "—" : data.FlightNumber;
            TailNumber   = string.IsNullOrWhiteSpace(data.TailNumber)   ? "—" : data.TailNumber;
            UpdateMilestones();
        });

    private void OnPhaseChanged(FlightState from, FlightState to, FlightData sample) =>
        OnUi(() =>
        {
            _currentPhase = to;
            UpdatePhaseHighlight(to);
            UpdateMilestones();
            SubmitPirepCommand.NotifyCanExecuteChanged();
        });

    private void OnConnectionChanged(bool connected) =>
        OnUi(() => IsConnected = connected);

    // ============================================================ Session
    private void OnFlightStateChanged(object? sender, PropertyChangedEventArgs e) =>
        OnUi(() =>
        {
            HasActiveSession = _flightState.HasActiveSession;
            if (HasActiveSession)
            {
                SessionHeaderText = $"Active session: {_flightState.FlightNumber} on {_flightState.Registration}"
                    + (_flightState.StartedAtUtc is { } t ? $" — started {t.UtcDateTime:HH:mm} UTC" : "");
                StartPosRepLoop();
            }
            else
            {
                SessionHeaderText = "No active flight session — open a booking and Start flight.";
                LastPosRepText = "—";
                _ = StopPosRepLoopAsync();
            }
            SubmitPirepCommand.NotifyCanExecuteChanged();
        });

    private void StartPosRepLoop()
    {
        if (_posrepTask is { IsCompleted: false }) return;
        _posrepCts = new CancellationTokenSource();
        var token = _posrepCts.Token;
        _posrepTask = Task.Run(() => PosRepLoopAsync(token), token);
    }

    private async Task StopPosRepLoopAsync()
    {
        if (_posrepCts is null) return;
        try { _posrepCts.Cancel(); } catch { /* ignore */ }
        if (_posrepTask is not null)
        {
            try { await _posrepTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected */ }
            catch { /* swallow */ }
        }
        _posrepCts.Dispose();
        _posrepCts = null;
        _posrepTask = null;
    }

    private async Task PosRepLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PosRepInterval);
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            if (_flightState.FlightSessionId is not { } sessionId) continue;
            if (_sim.LastSample is not { } sample) continue;

            // Persist the sample immediately; the queue's worker delivers it to the
            // server with retry/backoff. Enqueue never blocks on the network.
            var report = PositionReport.FromTelemetry(sample, _currentPhase);
            _posrepQueue.Enqueue(new PosrepEnvelope(sessionId, report));
        }
    }

    private void OnQueueChanged() =>
        OnUi(() =>
        {
            var pending = _posrepQueue.PendingCount;
            if (_posrepQueue.LastAckUtc is { } ack)
                LastPosRepText = ack.ToString("HH:mm:ss") + " UTC";
            QueueStatusText = pending == 0
                ? "All POSREPs sent"
                : $"{pending} POSREP{(pending == 1 ? "" : "s")} queued";
        });

    private void UpdateMilestones()
    {
        var sm = _sim.StateMachine;
        OffBlockText     = sm.OffBlockUtc?.ToString("HH:mm:ss") ?? "—";
        TakeoffText      = sm.TakeoffUtc?.ToString("HH:mm:ss")  ?? "—";
        LandingText      = sm.LandingUtc?.ToString("HH:mm:ss")  ?? "—";
        OnBlockText      = sm.OnBlockUtc?.ToString("HH:mm:ss")  ?? "—";
        BlockTimeText    = sm.BlockTime?.ToString(@"hh\:mm\:ss") ?? "—";
        AirTimeText      = sm.AirTime?.ToString(@"hh\:mm\:ss")   ?? "—";
        FuelUsedText     = sm.FuelUsedKg is { } f ? $"{f:N0} kg" : "—";
        LandingRateText  = sm.LandingRateFpm != 0 ? $"{sm.LandingRateFpm:F0} fpm" : "—";
    }

    private void UpdatePhaseHighlight(FlightState current)
    {
        foreach (var p in Phases)
        {
            p.IsCurrent = p.State == current;
            p.IsDone = p.State < current;   // enum order mirrors the flight sequence
        }
    }

    private static string FormatPhase(FlightState s) => s switch
    {
        FlightState.TaxiIn => "Taxi-in",
        _ => s.ToString()
    };

    private static void OnUi(Action action) =>
        Application.Current?.Dispatcher.Invoke(action);

    public void Dispose()
    {
        StopPosRepLoopAsync().GetAwaiter().GetResult();
        _posrepQueue.Changed -= OnQueueChanged;
        _posrepQueue.Dispose();
    }
}

public sealed partial class PhaseItem : ObservableObject
{
    public FlightState State { get; init; }
    public string Label { get; init; } = string.Empty;
    [ObservableProperty] private bool _isCurrent;
    [ObservableProperty] private bool _isDone;
}
