using AirSerbiaVirtua.Acars.Core;

namespace AirSerbiaVirtua.Tests.Unit.Desktop;

/// <summary>No-op sim bridge so SimulatorService can be constructed in tests.</summary>
public sealed class FakeSimBridge : ISimBridge
{
    public string Name => "Fake";
    public bool IsConnected => false;
    public event Action<string>? Log { add { } remove { } }
    public bool EnsureConnected() => false;
    public FlightData? ReadCurrent() => null;
    public void Close() { }
    public void Dispose() { }
}
