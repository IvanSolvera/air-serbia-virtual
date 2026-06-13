# Design — Downloadable ACARS client + website Download section

**Date:** 2026-06-13 · **Status:** SHIPPED (with deploy-time amendments below)

> **Amended at deploy (2026-06-13):** production turned out to be **systemd + Caddy**
> on `asv.solvera.one`, not Docker, with the API on its own subdomain. So vs. this
> spec: the client targets **`https://api-asv.solvera.one/`** (not `airserbiavirtual.rs`
> and not same-origin), and the installer is **hosted on the server** at
> `https://asv.solvera.one/downloads/AirSerbiaVirtua-ACARS-win-x64.exe` (Caddy
> `file_server`), **not GitHub Releases**. Authoritative current docs:
> `doc/release-acars.md` (build + upload) and `doc/update-runbook.md` (server deploy).
> The Download link also needed the `download` attribute to bypass Blazor enhanced nav.

## Goal

Let pilots download and run the desktop ACARS client from the public website. Ship
a single self-contained Windows executable that, out of the box, connects to the
**production** API at `https://airserbiavirtual.rs/` and lets a pilot sign in with
their ASV callsign.

## Decisions (locked)

| Decision | Choice |
|---|---|
| Packaging | **Single-file, self-contained, win-x64 `.exe`** (no .NET prereq, no admin install) |
| Hosting | **GitHub Releases** on the public repo `IvanSolvera/air-serbia-virtual` |
| Client API URL | `https://airserbiavirtual.rs/` (same-origin; client calls `{base}/api/v1/...` + `{base}/health`) |
| Asset name | `AirSerbiaVirtua-ACARS-win-x64.exe` (fixed across releases) |
| Website link | GitHub **latest-release** URL (stable, no per-release site change) |

Out of scope (note, not now): code signing (SmartScreen will warn — documented),
an Inno/MSI installer, and in-app auto-update.

---

## Component 1 — Desktop release build

**Single file.** Publish with:
```
dotnet publish AirSerbiaVirtua.Acars.Desktop -c Release -r win-x64 -p:Platform=x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:IncludeAllContentForSelfExtract=true \
  -p:AcarsApiBaseUrl=https://airserbiavirtual.rs/
```
Output: one `AirSerbiaVirtua.Acars.exe`, renamed to `AirSerbiaVirtua-ACARS-win-x64.exe`.
`IncludeAllContentForSelfExtract` bundles `appsettings.json` into the exe (App loads it
with `optional:false`, so it must be present at the self-extract dir at runtime); the
assembly-metadata overlay then wins for `Api:BaseUrl`.

**Baking the production API URL (no external file, no hardcoding in source).**
The desktop binds `Api:BaseUrl` from `appsettings.json` (dev = `http://localhost:5036/`)
into `ApiOptions`, and `SessionService` constructs `new ApiService(options.Value.BaseUrl)`.
We override it at *build* time without touching the dev file:

1. `AirSerbiaVirtua.Acars.Desktop.csproj` emits an assembly-metadata attribute when
   the `AcarsApiBaseUrl` property is supplied at publish:
   ```xml
   <ItemGroup Condition="'$(AcarsApiBaseUrl)' != ''">
     <AssemblyAttribute Include="System.Reflection.AssemblyMetadataAttribute">
       <_Parameter1>ApiBaseUrl</_Parameter1>
       <_Parameter2>$(AcarsApiBaseUrl)</_Parameter2>
     </AssemblyAttribute>
   </ItemGroup>
   ```
2. `App.xaml.cs` `ConfigureAppConfiguration`, *after* `AddJsonFile("appsettings.json")`,
   reads that metadata and, when present, layers it last so it wins:
   ```csharp
   var prod = Assembly.GetExecutingAssembly()
       .GetCustomAttributes<AssemblyMetadataAttribute>()
       .FirstOrDefault(a => a.Key == "ApiBaseUrl")?.Value;
   if (!string.IsNullOrWhiteSpace(prod))
       config.AddInMemoryCollection(new Dictionary<string,string?> { ["Api:BaseUrl"] = prod });
   ```
   - **Dev (F5, tests):** no property → no metadata → `appsettings.json` localhost used. Unchanged.
   - **Release publish:** metadata present → `Api:BaseUrl` = `https://airserbiavirtual.rs/`. The
     bundled `appsettings.json` is ignored for that key.

**Versioning.** Set `<Version>` (e.g. `1.0.0`) in the csproj so the file properties and the
titlebar (already shows the running version) identify the build. Bump per release.

**Why this is safe.** The only code change is an additive, guarded block in `App.xaml.cs`
startup; dev behaviour and all existing tests are unaffected (metadata is absent in dev).

---

## Component 2 — Website Download section

`AirSerbiaVirtua.Web` (Blazor SSR), styled to match the existing pages.

- **New page `/download`** (`Pages/Download.razor`):
  - Heading + one-paragraph "what is the ACARS client" blurb.
  - **System requirements:** Windows 10/11 (64-bit); MSFS 2020 or 2024 **+ FSUIPC7**
    for live flight telemetry; an approved ASV pilot account (sign in with your callsign).
  - Prominent **Download for Windows** button → the GitHub latest-release asset URL.
  - **Install/run steps:** download → run the `.exe` → (SmartScreen) *More info → Run anyway*
    (unsigned build) → sign in with your ASV callsign + password.
  - Small print: version line + "having trouble? contact ops".
- **Nav:** add `Download` to the header (Home / Schedules / Fleet / **Download** / Join us).
- **Download URL as one config value** with a stable default:
  `https://github.com/IvanSolvera/air-serbia-virtual/releases/latest/download/AirSerbiaVirtua-ACARS-win-x64.exe`
  (`/releases/latest/download/<asset>` always serves the newest release — no site change per release).
  Read from `Downloads:AcarsWindowsUrl` (web `appsettings.json`), defaulting to the above.

This page needs no API call (static content + external link), so it renders fine SSR with
the API down.

---

## Component 3 — Release runbook (`doc/release-acars.md`)

Per release:
1. Bump `<Version>`, build the single-file exe with `-p:AcarsApiBaseUrl=https://airserbiavirtual.rs/`.
2. (Optional) run it once locally and confirm it targets prod (`secure chip`/host shows airserbiavirtual.rs).
3. Create a GitHub Release (tag `acars-vX.Y.Z`) on the public repo and upload the exe as
   `AirSerbiaVirtua-ACARS-win-x64.exe` (via `gh release create` or the web UI).
4. Verify the website **Download** button downloads the asset; install on a clean Windows box;
   sign in; confirm it reaches the prod API.

The GitHub Release upload is performed by the maintainer (needs repo auth); everything else
(build, page, runbook) is in the repo.

---

## What gets built here vs. handed off

- **In this repo (I implement + verify locally):** csproj metadata + version, `App.xaml.cs`
  config overlay, the `/download` page + nav + config value, the release runbook. The
  single-file publish is built and verified locally (Windows x64); the existing test suites
  must stay green.
- **Maintainer action:** create the public GitHub Release and upload the `.exe`.

## Testing / verification

- `dotnet build` solution green; `Tests.Unit` (x64) still 38/38 (the `App.xaml.cs` change is
  not covered by tests but is guarded + additive — verified by a Release single-file publish
  that launches and shows the prod host).
- `dotnet publish` (single-file) succeeds and produces one exe; launching it shows the API
  host as `airserbiavirtual.rs`.
- Web builds; `/download` renders with the correct GitHub link; nav shows Download.

## Open risks

- **Unsigned exe** → SmartScreen warning. Documented on the page; code signing is a future
  add (needs a cert).
- **Same-origin assumption:** `https://airserbiavirtual.rs/api/v1/...` and `/health` must be
  routed to the API by the host proxy (as the web portal already relies on). Confirm
  `https://airserbiavirtual.rs/health` responds before announcing the download.
