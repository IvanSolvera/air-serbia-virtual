# Release runbook — ACARS Windows client

Builds the single-file ACARS desktop client (pointing at **production**) and publishes
it as a GitHub Release so the website **Download** button serves it. Run on a Windows
x64 machine with the .NET 10 SDK.

## 1. Build the distributable

From the repo root (PowerShell):

```powershell
dotnet publish AirSerbiaVirtua.Acars.Desktop\AirSerbiaVirtua.Acars.Desktop.csproj `
  -c Release -r win-x64 -p:Platform=x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:IncludeAllContentForSelfExtract=true `
  -p:AcarsApiBaseUrl=https://airserbiavirtual.rs/ `
  -o publish\acars
```

Produces ONE file: `publish\acars\AirSerbiaVirtua.Acars.Desktop.exe` (~180 MB, self-contained).
`-p:AcarsApiBaseUrl` bakes the production API URL in via assembly metadata, so the client
connects to `https://airserbiavirtual.rs/` out of the box. Rename it to the asset name the
website links to:

```powershell
Rename-Item publish\acars\AirSerbiaVirtua.Acars.Desktop.exe AirSerbiaVirtua-ACARS-win-x64.exe
```

## 2. Smoke-test (recommended)

Run the exe; the login screen's secure chip / API host should read `airserbiavirtual.rs`.
Sign in with a real ASV callsign and confirm bookings/briefing load.

## 3. Publish the GitHub Release (public repo `IvanSolvera/air-serbia-virtual`)

Tag matches `<Version>` in the csproj. Using the gh CLI:

```powershell
gh release create acars-v1.0.0 `
  publish\acars\AirSerbiaVirtua-ACARS-win-x64.exe `
  --repo IvanSolvera/air-serbia-virtual `
  --title "ACARS client v1.0.0" `
  --notes "Air Serbia Virtual ACARS desktop client for Windows (64-bit). Download, run, sign in with your ASV callsign."
```

…or via the web UI: **Releases → Draft a new release** → tag `acars-v1.0.0` → attach the
`.exe` → Publish.

> **The asset name MUST be `AirSerbiaVirtua-ACARS-win-x64.exe`.** The website button links to
> `https://github.com/IvanSolvera/air-serbia-virtual/releases/latest/download/AirSerbiaVirtua-ACARS-win-x64.exe`,
> which always serves the newest release's asset of that name — so no website change is
> needed per release.

## 4. Verify

- `https://airserbiavirtual.rs/download` → **Download** button → the file downloads.
- The downloaded exe runs and connects to production.

## Notes

- **Unsigned build** → Windows SmartScreen warns on first run (*More info → Run anyway*).
  Code signing (OV/EV cert) removes this — future enhancement.
- **Bump the version each release:** edit `<Version>` in
  `AirSerbiaVirtua.Acars.Desktop.csproj`, rebuild, and use a matching `acars-vX.Y.Z` tag.
- The download URL is config-driven on the site (`Downloads:AcarsWindowsUrl` in the web
  `appsettings.json`) but defaults to the stable latest-release URL above.
- Desktop clients are per-pilot installs and are independent of the server deploy.
