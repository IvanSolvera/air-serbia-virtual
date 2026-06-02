# AirSerbiaVirtua.Acars.PoC

Proof-of-concept ACARS desktop client for Air Serbia Virtua. Console app today;
will be ported to WPF/hybrid later.

## What it does
- `FsuipcService` — robust wrapper over Paul Henty's **FSUIPCClientDLL**:
  auto-detecting `Open()`, reconnection if the sim drops, all required offsets,
  and fuel via `PayloadServices.FuelWeightKgs`. Raw offsets are converted to
  knots / feet / fpm / decimal degrees / kg.
- `FlightStateMachine` — phase tracking
  `Preflight → Taxi → Takeoff → Climb → Cruise → Descent → Landing → TaxiIn → Arrived`,
  capturing OffBlock/Takeoff/Landing/OnBlock times, **landing rate at touchdown**,
  and computing block time, air time and fuel used.
- `ApiService` — typed HTTPS client for the API: login (stores bearer token),
  airport delta sync, flight start, 30-second POSREP push, and PIREP submission.

## Run
```bash
dotnet run                 # live FSUIPC polling at 1 Hz (requires sim + FSUIPC)
dotnet run -- --selftest   # deterministic state-machine test with mock data
```
Build x64 (set in the csproj) so the IPC bridge matches FSUIPC7 / MSFS.

## Offsets
| Field            | Offset  | Raw unit            | Converted     |
|------------------|---------|---------------------|---------------|
| Latitude         | 0x0560  | 64-bit fixed point  | decimal deg   |
| Longitude        | 0x0568  | 64-bit fixed point  | decimal deg   |
| Altitude         | 0x0570  | metres (32.32)      | feet          |
| IAS              | 0x02BC  | knots × 128         | knots         |
| Ground speed     | 0x02B4  | (m/s) × 65536       | knots         |
| Vertical speed   | 0x02C8  | (m/s) × 256         | fpm           |
| On ground        | 0x0366  | 0/1                 | bool          |
| Parking brake    | 0x0BC8  | 0 / 32767           | bool          |
| Flight number    | 0x3130  | string (12)         | string        |
| Tail / reg       | 0x313C  | string (12)         | string        |
| Fuel             | PayloadServices.FuelWeightKgs            | kg |
