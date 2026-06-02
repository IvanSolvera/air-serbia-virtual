using FSUIPC;

namespace AirSerbiaVirtua.Acars.Core;

/// <summary>
/// Robust wrapper around Paul Henty's FSUIPCClientDLL. Owns the connection
/// lifecycle (open / auto-reconnect / close), declares the required offsets,
/// processes them once per poll and exposes a converted <see cref="FlightData"/>.
///
/// All numeric offsets are read as raw simulator units and converted to
/// standard aviation units in <see cref="ReadCurrent"/>.
/// </summary>
public sealed class FsuipcService : IDisposable
{
    // ---- Unit conversion constants ------------------------------------------
    private const double MetresToFeet = 3.280839895;
    private const double MetresPerSecToKnots = 1.943844492;
    private const double MetresPerSecToFpm = 196.8503937; // 60 / 0.3048

    // 64-bit fixed-point scaling helpers.
    private const double TwoPow32 = 4294967296.0;          // 65536 * 65536
    private const double LatScale = 90.0 / (10001750.0 * TwoPow32);
    private const double LonScale = 360.0 / (TwoPow32 * TwoPow32);

    // ---- Offsets (auto-register with the FSUIPC default group on creation) --
    // Position / altitude â€” 8-byte fixed point.
    private readonly Offset<long> _latitude = new(0x0560);   // FsLatitude
    private readonly Offset<long> _longitude = new(0x0568);  // FsLongitude
    private readonly Offset<long> _altitude = new(0x0570);   // FsAltitude (metres, 32.32 fixed)

    // Speeds â€” 4-byte integers.
    private readonly Offset<int> _ias = new(0x02BC);   // IAS  : knots * 128
    private readonly Offset<int> _gs = new(0x02B4);    // GS   : (m/s) * 65536
    private readonly Offset<int> _vs = new(0x02C8);    // VS   : (m/s) * 256

    // Status â€” 2-byte integers.
    private readonly Offset<short> _onGround = new(0x0366);      // 0 = airborne, 1 = on ground
    private readonly Offset<short> _parkingBrake = new(0x0BC8);  // 0 = off, 32767 = on

    // ATC info â€” fixed-length strings.
    private readonly Offset<string> _flightNumber = new(0x3130, 12);
    private readonly Offset<string> _tailNumber = new(0x313C, 12);

    public bool IsConnected { get; private set; }

    /// <summary>Raised on connection state changes and recoverable errors.</summary>
    public event Action<string>? Log;

    /// <summary>
    /// Ensures a live connection, opening (with auto-detection) if needed.
    /// Safe to call every poll: it is a cheap no-op when already connected.
    /// Returns true if connected.
    /// </summary>
    public bool EnsureConnected()
    {
        if (IsConnected && FSUIPCConnection.IsOpen)
            return true;

        try
        {
            // No argument => auto-detect the running sim (FSUIPC4/5/6 or FSUIPC7/MSFS).
            FSUIPCConnection.Open();
            IsConnected = FSUIPCConnection.IsOpen;
            if (IsConnected)
                Log?.Invoke($"Connected to {FSUIPCConnection.FlightSimVersionConnected} via FSUIPC.");
            return IsConnected;
        }
        catch (FSUIPCException ex)
        {
            IsConnected = false;
            Log?.Invoke($"Connect failed ({ex.FSUIPCErrorCode}): {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            Log?.Invoke($"Connect failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Processes the registered offsets and returns the converted snapshot.
    /// Returns null if not connected or if the sim dropped (in which case the
    /// connection is closed so the next <see cref="EnsureConnected"/> reopens it).
    /// </summary>
    public FlightData? ReadCurrent()
    {
        if (!EnsureConnected())
            return null;

        try
        {
            FSUIPCConnection.Process();

            // Fuel comes from the higher-level PayloadServices helper.
            double fuelKg = 0;
            try
            {
                FSUIPCConnection.PayloadServices.RefreshData();
                fuelKg = FSUIPCConnection.PayloadServices.FuelWeightKgs;
            }
            catch (FSUIPCException ex)
            {
                Log?.Invoke($"Fuel read failed ({ex.FSUIPCErrorCode}); reporting 0 kg.");
            }

            double altMetres = _altitude.Value / TwoPow32;

            return new FlightData
            {
                LatitudeDeg = _latitude.Value * LatScale,
                LongitudeDeg = _longitude.Value * LonScale,
                AltitudeFt = altMetres * MetresToFeet,
                IndicatedAirspeedKts = _ias.Value / 128.0,
                GroundSpeedKts = (_gs.Value / 65536.0) * MetresPerSecToKnots,
                VerticalSpeedFpm = (_vs.Value / 256.0) * MetresPerSecToFpm,
                OnGround = _onGround.Value != 0,
                ParkingBrakeSet = _parkingBrake.Value > 0,
                FlightNumber = Clean(_flightNumber.Value),
                TailNumber = Clean(_tailNumber.Value),
                FuelKg = fuelKg
            };
        }
        catch (FSUIPCException ex)
        {
            // Sim closed / IPC lost â€” drop the connection and let the loop retry.
            Log?.Invoke($"Lost connection ({ex.FSUIPCErrorCode}): {ex.Message}. Will reconnect.");
            Close();
            return null;
        }
    }

    private static string Clean(string? raw) =>
        string.IsNullOrEmpty(raw) ? string.Empty : raw.Split('\0')[0].Trim();

    public void Close()
    {
        try
        {
            if (FSUIPCConnection.IsOpen)
                FSUIPCConnection.Close();
        }
        catch
        {
            // Closing a dead connection can throw; ignore.
        }
        finally
        {
            IsConnected = false;
        }
    }

    public void Dispose() => Close();
}
