using AirSerbiaVirtua.Acars.Core;
using AirSerbiaVirtua.Acars.PoC;

// `--selftest` runs the deterministic state-machine simulation with mock data
// (no simulator required). Otherwise the live FSUIPC polling loop runs.
if (args.Contains("--selftest"))
    return FlightSimulationTest.Run();

Console.WriteLine("Air Serbia Virtua — ACARS PoC");
Console.WriteLine("Polling FSUIPC at 1 Hz. Press Ctrl+C to exit.\n");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

using var fsuipc = new FsuipcService();
fsuipc.Log += msg => Console.WriteLine($"[FSUIPC] {msg}");

var stateMachine = new FlightStateMachine();
stateMachine.StateChanged += (prev, next, sample) =>
{
    Console.WriteLine($"\n>>> PHASE: {prev} -> {next} at {sample.SampleTimeUtc:HH:mm:ss} UTC\n");
    if (next == FlightState.Landing)
        Console.WriteLine($"    Landing rate: {stateMachine.LandingRateFpm:F0} fpm\n");
};

using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
try
{
    while (await timer.WaitForNextTickAsync(cts.Token))
    {
        var data = fsuipc.ReadCurrent();
        if (data is null)
        {
            Console.WriteLine($"[{DateTimeOffset.UtcNow:HH:mm:ss}] Waiting for simulator / FSUIPC...");
            continue;
        }

        Console.WriteLine(data.ToConsoleBlock());
        stateMachine.Update(data);
    }
}
catch (OperationCanceledException)
{
    // Normal Ctrl+C shutdown.
}

Console.WriteLine("\nShutting down. Flight summary:");
Console.WriteLine($"  State      : {stateMachine.State}");
Console.WriteLine($"  Block time : {stateMachine.BlockTime}");
Console.WriteLine($"  Air time   : {stateMachine.AirTime}");
Console.WriteLine($"  Fuel used  : {stateMachine.FuelUsedKg:F1} kg");
Console.WriteLine($"  Landing    : {stateMachine.LandingRateFpm:F0} fpm");
return 0;
