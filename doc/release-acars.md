# Release runbook — ACARS Windows client

Builds the single-file ACARS desktop client (pointing at **production**,
`https://api-asv.solvera.one/`) and publishes it to the server so the website
**Download** button serves it from `https://asv.solvera.one/downloads/`.

Build on a **Windows x64** machine with the .NET 10 SDK.

## 1. Build the distributable

From the repo root (PowerShell):

```powershell
dotnet publish AirSerbiaVirtua.Acars.Desktop\AirSerbiaVirtua.Acars.Desktop.csproj `
  -c Release -r win-x64 -p:Platform=x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:IncludeAllContentForSelfExtract=true `
  -p:AcarsApiBaseUrl=https://api-asv.solvera.one/ `
  -o publish\acars
Rename-Item publish\acars\AirSerbiaVirtua.Acars.Desktop.exe AirSerbiaVirtua-ACARS-win-x64.exe
```

Produces ONE ~180 MB file. `-p:AcarsApiBaseUrl` bakes the API URL in via assembly
metadata, so the client connects to `api-asv.solvera.one` out of the box. The dev
`appsettings.json` (localhost) is untouched. Confirm the baked URL if you like:

```powershell
Get-ChildItem -Recurse AirSerbiaVirtua.Acars.Desktop\obj\x64\Release -Filter *.AssemblyInfo.cs |
  ForEach-Object { (Select-String $_.FullName -Pattern 'ApiBaseUrl"\s*,\s*"([^"]+)"').Matches.Groups[1].Value } | Sort-Object -Unique
# -> https://api-asv.solvera.one/
```

> Do a clean rebuild (`Remove-Item AirSerbiaVirtua.Acars.Desktop\obj,bin,publish\acars -Recurse -Force`)
> if you've previously built with a different `AcarsApiBaseUrl`, so no stale URL is bundled.

## 2. Upload to the server

The site serves `/downloads/*` from `/var/www/asv-downloads` (Caddy `file_server`).
Copy the exe there (from the Windows build box, or scp from anywhere with the key):

```powershell
scp publish\acars\AirSerbiaVirtua-ACARS-win-x64.exe root@13.140.150.148:/var/www/asv-downloads/
ssh root@13.140.150.148 "chmod 644 /var/www/asv-downloads/AirSerbiaVirtua-ACARS-win-x64.exe; sha256sum /var/www/asv-downloads/AirSerbiaVirtua-ACARS-win-x64.exe"
```

Confirm the server SHA-256 matches your local
`(Get-FileHash publish\acars\AirSerbiaVirtua-ACARS-win-x64.exe -Algorithm SHA256).Hash`.

No restart is needed — Caddy serves the file as-is. The website Download button and
`Downloads:AcarsWindowsUrl` already point at
`https://asv.solvera.one/downloads/AirSerbiaVirtua-ACARS-win-x64.exe`.

## 3. Verify

```powershell
Invoke-WebRequest https://asv.solvera.one/downloads/AirSerbiaVirtua-ACARS-win-x64.exe -Method Head |
  Select-Object -ExpandProperty Headers   # 200, Content-Length ~187 MB
```
Then in a browser: `https://asv.solvera.one/download` → click **Download for Windows**
→ the .exe saves → run it → it should connect to `api-asv.solvera.one` and let you
sign in with your ASV callsign.

## Notes

- **Unsigned build** → Windows SmartScreen warns on first run (*More info → Run anyway*).
  Code signing (OV/EV cert) removes this — future enhancement.
- The download link uses the `download` attribute so Blazor's enhanced navigation
  doesn't intercept the click. Keep that attribute on the button.
- Caddy serves the file directly; there is no GitHub Release in this setup.
- Desktop clients are per-pilot installs, independent of the server deploy.
