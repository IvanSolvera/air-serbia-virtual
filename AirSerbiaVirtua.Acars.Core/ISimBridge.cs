namespace AirSerbiaVirtua.Acars.Core;

/// <summary>
/// Abstraction over a flight-simulator connection. The flight logic
/// (<see cref="SimulatorService"/>, <see cref="FlightStateMachine"/>) depends
/// only on this contract, never on a concrete simulator API, so a second sim
/// (e.g. X-Plane) is a new implementation rather than a rewrite.
///
/// Inspired by the Global Virtual Airlines Group ACARS <c>Bridge</c> interface
/// (FSUIPC and X-Plane behind one contract) — see <c>doc/acars-comparison.md</c>.
/// Implementations are owned by a single poll loop and are not thread-safe.
/// </summary>
public interface ISimBridge : IDisposable
{
    /// <summary>Short human-readable name of the bridge, e.g. "FSUIPC". Used in log prefixes.</summary>
    string Name { get; }

    /// <summary>Whether a live connection to the simulator is currently held.</summary>
    bool IsConnected { get; }

    /// <summary>Raised on connection state changes and recoverable errors.</summary>
    event Action<string>? Log;

    /// <summary>
    /// Ensures a live connection, opening (with auto-detection) if needed.
    /// Safe to call every poll: a cheap no-op when already connected.
    /// Returns true if connected.
    /// </summary>
    bool EnsureConnected();

    /// <summary>
    /// Reads the current telemetry snapshot. Returns null if not connected or
    /// if the sim dropped (the connection is then closed so the next
    /// <see cref="EnsureConnected"/> reopens it).
    /// </summary>
    FlightData? ReadCurrent();

    /// <summary>Closes the connection if open. Safe to call when already closed.</summary>
    void Close();
}
