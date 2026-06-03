namespace AirSerbiaVirtua.Acars.Core;

/// <summary>
/// Long-lived background simulator binding. Owns an <see cref="ISimBridge"/>
/// and a <see cref="FlightStateMachine"/>, runs a 1 Hz poll loop on a worker
/// task, and raises events for telemetry samples, phase transitions and
/// connection state changes.
///
/// Events fire on the worker thread — UI consumers must marshal to their
/// own dispatcher.
/// </summary>
public sealed class SimulatorService : IDisposable
{
    private readonly ISimBridge _sim;
    public FlightStateMachine StateMachine { get; } = new();

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public bool IsConnected => _sim.IsConnected;
    public bool IsRunning => _loopTask is { IsCompleted: false };
    public FlightData? LastSample { get; private set; }

    public event Action<FlightData>? TelemetryUpdated;
    public event Action<FlightState, FlightState, FlightData>? PhaseChanged;
    public event Action<bool>? ConnectionStateChanged;
    public event Action<string>? Log;

    /// <param name="bridge">
    /// Simulator bridge to use. Defaults to a real <see cref="FsuipcService"/>;
    /// pass a fake to drive the service from recorded/mock telemetry in tests.
    /// </param>
    public SimulatorService(ISimBridge? bridge = null)
    {
        _sim = bridge ?? new FsuipcService();
        _sim.Log += msg => Log?.Invoke($"[{_sim.Name}] {msg}");
        StateMachine.StateChanged += (from, to, sample) => PhaseChanged?.Invoke(from, to, sample);
    }

    /// <summary>Idempotent — starts the poll loop if not already running.</summary>
    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loopTask = Task.Run(() => LoopAsync(token), token);
    }

    /// <summary>Resets the captured phases/milestones for a new flight session.</summary>
    public void ResetSession()
    {
        StateMachine.Reset();
        LastSample = null;
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        try { _cts.Cancel(); } catch { /* ignore */ }
        if (_loopTask is not null)
        {
            try { await _loopTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex) { Log?.Invoke($"Loop stopped with: {ex.Message}"); }
        }
        _cts.Dispose();
        _cts = null;
        _loopTask = null;
        _sim.Close();
        if (IsConnectedSnapshotChanged(false))
            ConnectionStateChanged?.Invoke(false);
    }

    private bool _lastConnected;
    private bool IsConnectedSnapshotChanged(bool newValue)
    {
        if (_lastConnected == newValue) return false;
        _lastConnected = newValue;
        return true;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            FlightData? data = null;
            try { data = _sim.ReadCurrent(); }
            catch (Exception ex) { Log?.Invoke($"Poll error: {ex.Message}"); }

            if (IsConnectedSnapshotChanged(_sim.IsConnected))
                ConnectionStateChanged?.Invoke(_sim.IsConnected);

            if (data is null) continue;

            LastSample = data;
            StateMachine.Update(data);
            TelemetryUpdated?.Invoke(data);
        }
    }

    public void Dispose()
    {
        try { StopAsync().GetAwaiter().GetResult(); } catch { /* best effort */ }
        _sim.Dispose();
    }
}
