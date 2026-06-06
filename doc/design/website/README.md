# Air Serbia Virtual — Flight Crew Center

Static, single-page community dashboard for a flight-sim virtual airline.
Plain HTML + Tailwind (CDN) + vanilla JS. No build step required.

## Structure
```
air-serbia-virtual/
├── index.html      # Main dashboard (8 tabs)
├── app.js          # All UI logic, mock data, logo mark, render functions
├── logo.html       # Standalone logo system / copyable SVG lockups
└── assets/         # Aircraft photos (hero + fleet cards)
    ├── a319.png
    ├── atr72.jpg
    └── a330.png
```

## Run locally
It's fully static — just open `index.html` in a browser, or serve the folder:
```
# any static server, e.g.
npx serve .
# or python
python -m http.server 5500
```
In Rider you can use the built-in static file preview, or attach it to an
ASP.NET project's `wwwroot/`.

## Tabs
Home · Live Flights · Schedules · Profile · Fleet · Dispatch · Downloads · Heritage

## Wiring to a real API (notes for the .NET backend)
The mock data arrays in `app.js` map cleanly to API endpoints:

| `app.js` constant | Suggested endpoint            |
|-------------------|-------------------------------|
| `FLIGHTS`         | `GET /api/flights/live`       |
| `SCHEDULES`       | `GET /api/schedules`          |
| `FLEET`           | `GET /api/fleet`              |
| `PILOT_STATS` etc | `GET /api/pilots/{id}`        |
| `ACHIEVEMENTS`    | `GET /api/pilots/{id}/awards` |
| `genPlan()`       | `POST /api/dispatch/ofp`      |

Replace the `const X = [...]` literals with `await fetch(...)` calls and keep
the existing `render*()` functions as-is.

## Branding
The logo is an **original community mark** (geometric twin-wing). It is
intentionally NOT the official Air Serbia trademark. This project is a
non-commercial fan/community build and is not affiliated with Air Serbia a.d.
