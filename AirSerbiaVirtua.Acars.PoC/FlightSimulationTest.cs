using AirSerbiaVirtua.Acars.Core;

namespace AirSerbiaVirtua.Acars.PoC;

/// <summary>
/// Dependency-free simulation harness for <see cref="FlightStateMachine"/>.
/// Feeds a scripted sequence of mock <see cref="FlightData"/> samples (1 Hz) and
/// asserts the phase progression and computed metrics. Run via <c>--selftest</c>.
/// Returns the number of failed assertions (0 = pass).
/// </summary>
public static class FlightSimulationTest
{
    public static int Run()
    {
        Console.WriteLine("=== FlightStateMachine self-test (mock data) ===\n");

        var t0 = new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero);
        int second = 0;
        var samples = new List<FlightData>();

        // Helper to append a 1-second sample.
        void Add(double gs, double vs, double altFt, bool onGround, bool brake, double fuel, double ias = 0)
            => samples.Add(new FlightData
            {
                SampleTimeUtc = t0.AddSeconds(second++),
                GroundSpeedKts = gs,
                VerticalSpeedFpm = vs,
                AltitudeFt = altFt,
                OnGround = onGround,
                ParkingBrakeSet = brake,
                FuelKg = fuel,
                IndicatedAirspeedKts = ias,
                FlightNumber = "JU360",
                TailNumber = "YU-API",
                LatitudeDeg = 44.8184,
                LongitudeDeg = 20.3091
            });

        // Preflight: parked, brake set (2 s).
        for (int i = 0; i < 2; i++) Add(gs: 0, vs: 0, altFt: 335, onGround: true, brake: true, fuel: 9000);

        // Off-block: brake released, rolling — should enter Taxi, capture OffBlock + FuelStart=9000.
        for (int i = 0; i < 3; i++) Add(gs: 12, vs: 0, altFt: 335, onGround: true, brake: false, fuel: 8980);

        // Takeoff roll then wheels-up (on-ground 1→0) — capture Takeoff.
        Add(gs: 120, vs: 0, altFt: 335, onGround: true, brake: false, fuel: 8950, ias: 145);
        Add(gs: 150, vs: 1800, altFt: 360, onGround: false, brake: false, fuel: 8940, ias: 160); // airborne -> Takeoff -> Climb

        // Climb toward planned FL (35000 ft used below).
        for (int i = 0; i < 6; i++) Add(gs: 300, vs: 2200, altFt: 5000 + i * 5000, onGround: false, brake: false, fuel: 8800 - i * 30);

        // Reach cruise altitude (>= 35000-200) — enter Cruise.
        for (int i = 0; i < 4; i++) Add(gs: 440, vs: 50, altFt: 35000, onGround: false, brake: false, fuel: 8500 - i * 10);

        // Sustained descent < -300 fpm for >=15 s — enter Descent.
        for (int i = 0; i < 18; i++) Add(gs: 380, vs: -1500, altFt: 35000 - i * 1500, onGround: false, brake: false, fuel: 8400 - i * 5);

        // Approach to touchdown: last airborne sample VS = -240, then on-ground (1) — capture LandingRate.
        Add(gs: 140, vs: -240, altFt: 60, onGround: false, brake: false, fuel: 8200, ias: 138);
        Add(gs: 135, vs: 0, altFt: 25, onGround: true, brake: false, fuel: 8195, ias: 132); // touchdown -> Landing

        // Rollout: GS drops below 30 on ground — enter TaxiIn.
        Add(gs: 60, vs: 0, altFt: 25, onGround: true, brake: false, fuel: 8190);
        Add(gs: 20, vs: 0, altFt: 25, onGround: true, brake: false, fuel: 8188);

        // Taxi to gate, then stop + brake set — enter Arrived, capture OnBlock + FuelEnd=8170.
        for (int i = 0; i < 2; i++) Add(gs: 8, vs: 0, altFt: 25, onGround: true, brake: false, fuel: 8180);
        Add(gs: 0, vs: 0, altFt: 25, onGround: true, brake: true, fuel: 8170);

        // Run the machine.
        var sm = new FlightStateMachine { PlannedCruiseAltFt = 35000 };
        var visited = new List<FlightState> { sm.State };
        sm.StateChanged += (prev, next, s) =>
        {
            visited.Add(next);
            Console.WriteLine($"  [{s.SampleTimeUtc:HH:mm:ss}] {prev,-9} -> {next}");
        };

        foreach (var sample in samples)
            sm.Update(sample);

        // ---- Assertions -----------------------------------------------------
        int failures = 0;
        void Check(string name, bool ok, string detail = "")
        {
            Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}{(detail.Length > 0 ? $"  ({detail})" : "")}");
            if (!ok) failures++;
        }

        Console.WriteLine("\n--- Assertions ---");

        var expectedPath = new[]
        {
            FlightState.Preflight, FlightState.Taxi, FlightState.Takeoff, FlightState.Climb,
            FlightState.Cruise, FlightState.Descent, FlightState.Landing, FlightState.TaxiIn, FlightState.Arrived
        };
        Check("Phase progression matches expected pipeline",
            visited.SequenceEqual(expectedPath),
            string.Join(" -> ", visited));

        Check("Final state is Arrived", sm.State == FlightState.Arrived);
        Check("OffBlock captured", sm.OffBlockUtc is not null);
        Check("Takeoff captured", sm.TakeoffUtc is not null);
        Check("Landing captured", sm.LandingUtc is not null);
        Check("OnBlock captured", sm.OnBlockUtc is not null);

        Check("Landing rate captured at touchdown (-240 fpm)",
            Math.Abs(sm.LandingRateFpm - (-240)) < 0.001, $"{sm.LandingRateFpm:F0} fpm");

        Check("Block time positive & > air time",
            sm.BlockTime is { } bt && sm.AirTime is { } at && bt > at,
            $"block={sm.BlockTime}, air={sm.AirTime}");

        Check("Fuel used = 8980 (off-block) - 8170 = 810 kg",
            sm.FuelUsedKg is { } fu && Math.Abs(fu - 810) < 0.001, $"{sm.FuelUsedKg:F1} kg");

        Console.WriteLine($"\n=== {(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)")} ===");
        Console.WriteLine($"Block time : {sm.BlockTime}");
        Console.WriteLine($"Air time   : {sm.AirTime}");
        Console.WriteLine($"Fuel used  : {sm.FuelUsedKg:F1} kg");
        Console.WriteLine($"Landing    : {sm.LandingRateFpm:F0} fpm");

        return failures;
    }
}
