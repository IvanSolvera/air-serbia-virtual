/* ===== Air Serbia Virtual — Flight Crew Center ===== */

/* ---- ASV twin-wing mark (original community logo) ---- */
function emblemSVG(id) {
  return `
<svg viewBox="0 0 120 120" width="100%" height="100%" aria-hidden="true">
  <defs>
    <linearGradient id="emb${id}" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#1b3e6e"/><stop offset="1" stop-color="#08152e"/>
    </linearGradient>
  </defs>
  <rect x="2" y="2" width="116" height="116" rx="28" fill="url(#emb${id})" stroke="#ffffff" stroke-opacity="0.12" stroke-width="1.5"/>
  <g fill="none" stroke-linecap="round" stroke-linejoin="round">
    <path d="M27 58 L46 40 L60 51 L74 40 L93 58" stroke="#F4F7FB" stroke-width="8.5"/>
    <path d="M22 77 L60 58 L98 77" stroke="#F4F7FB" stroke-width="8"/>
    <path d="M34 91 L60 77 L86 91" stroke="#D8203F" stroke-width="7.5"/>
  </g>
</svg>`;
}
function mountEmblems() {
  document.querySelectorAll('.emblem, .emblem-sm').forEach((el, i) => {
    el.style.background = 'transparent';
    el.style.border = 'none';
    el.innerHTML = emblemSVG(i);
  });
}

/* ---- UTC clock ---- */
function tickClock() {
  const d = new Date();
  const hh = String(d.getUTCHours()).padStart(2, '0');
  const mm = String(d.getUTCMinutes()).padStart(2, '0');
  const ss = String(d.getUTCSeconds()).padStart(2, '0');
  const el = document.getElementById('utcClock');
  if (el) el.textContent = `${hh}:${mm}:${ss} UTC`;
}

/* ---- Tab switching ---- */
function setTab(name) {
  document.querySelectorAll('.view').forEach(v => v.classList.remove('active'));
  const view = document.getElementById('view-' + name);
  if (view) view.classList.add('active');
  document.querySelectorAll('[role="tablist"] .tab-btn').forEach(b => {
    b.setAttribute('aria-selected', b.dataset.tab === name ? 'true' : 'false');
  });
  window.scrollTo({ top: 0, behavior: 'smooth' });
}
document.addEventListener('click', e => {
  const t = e.target.closest('[data-tab]');
  if (t) { e.preventDefault(); setTab(t.dataset.tab); }
});

/* ===================== DATA ===================== */
const STATS = [
  { icon: 'users', label: 'Active Pilots', value: 1284, fmt: 'n', sub: '+37 this week', accent: 'royal' },
  { icon: 'timer', label: 'Hours Flown', value: 92840, fmt: 'n', sub: 'network total', accent: 'crimson' },
  { icon: 'plane', label: 'Active Flights', value: 18, fmt: 'n', sub: 'airborne now', accent: 'royal' },
  { icon: 'globe-2', label: 'Destinations', value: 63, fmt: 'n', sub: 'across 4 continents', accent: 'crimson' },
];

const ROUTES = [
  { pair: 'BEG → CDG', name: 'Belgrade · Paris', pct: 92 },
  { pair: 'BEG → IST', name: 'Belgrade · Istanbul', pct: 81 },
  { pair: 'BEG → FRA', name: 'Belgrade · Frankfurt', pct: 74 },
  { pair: 'BEG → JFK', name: 'Belgrade · New York', pct: 58 },
  { pair: 'BEG → TIV', name: 'Belgrade · Tivat', pct: 49 },
];

const STATUS = {
  enroute:   { label: 'En Route',  cls: 'bg-emerald-400/15 text-emerald-300 border-emerald-400/30', dot: 'bg-emerald-400' },
  boarding:  { label: 'Boarding',  cls: 'bg-amber-400/15 text-amber-300 border-amber-400/30',       dot: 'bg-amber-400' },
  scheduled: { label: 'Scheduled', cls: 'bg-slate-400/15 text-slate-300 border-slate-400/30',       dot: 'bg-slate-400' },
  landed:    { label: 'Landed',    cls: 'bg-royal/20 text-royal-bright border-royal/40',            dot: 'bg-royal-bright' },
};

const FLIGHTS = [
  { no: 'ASV451', dep: 'BEG', arr: 'CDG', depCity: 'Belgrade', arrCity: 'Paris CDG',     ac: 'A320',     pilot: 'M. Ilić',     dt: '08:10', st: 'enroute',   prog: 64, fl: '360' },
  { no: 'ASV902', dep: 'BEG', arr: 'JFK', depCity: 'Belgrade', arrCity: 'New York JFK',  ac: 'A330-200', pilot: 'J. Petrović', dt: '06:45', st: 'enroute',   prog: 41, fl: '380' },
  { no: 'ASV118', dep: 'BEG', arr: 'IST', depCity: 'Belgrade', arrCity: 'Istanbul',      ac: 'A319',     pilot: 'D. Novak',    dt: '09:05', st: 'enroute',   prog: 88, fl: '350' },
  { no: 'ASV330', dep: 'TIV', arr: 'BEG', depCity: 'Tivat',    arrCity: 'Belgrade',      ac: 'ATR 72',   pilot: 'S. Marić',    dt: '09:20', st: 'enroute',   prog: 22, fl: '210' },
  { no: 'ASV220', dep: 'BEG', arr: 'FRA', depCity: 'Belgrade', arrCity: 'Frankfurt',     ac: 'A319',     pilot: 'A. Kovač',    dt: '09:40', st: 'boarding',  prog: 0,  fl: '—' },
  { no: 'ASV512', dep: 'BEG', arr: 'FCO', depCity: 'Belgrade', arrCity: 'Rome FCO',      ac: 'A320',     pilot: 'L. Đorđević', dt: '10:15', st: 'boarding',  prog: 0,  fl: '—' },
  { no: 'ASV640', dep: 'BEG', arr: 'ZRH', depCity: 'Belgrade', arrCity: 'Zürich',        ac: 'A319',     pilot: 'N. Pavlović', dt: '11:00', st: 'scheduled', prog: 0,  fl: '—' },
  { no: 'ASV808', dep: 'BEG', arr: 'ATH', depCity: 'Belgrade', arrCity: 'Athens',        ac: 'A320',     pilot: 'V. Stojanović', dt: '11:35', st: 'scheduled', prog: 0, fl: '—' },
  { no: 'ASV077', dep: 'OTP', arr: 'BEG', depCity: 'Bucharest', arrCity: 'Belgrade',     ac: 'ATR 72',   pilot: 'B. Lukić',    dt: '07:30', st: 'landed',    prog: 100, fl: '—' },
  { no: 'ASV301', dep: 'BEG', arr: 'TGD', depCity: 'Belgrade', arrCity: 'Podgorica',     ac: 'ATR 72',   pilot: 'K. Jovanović', dt: '07:05', st: 'landed',   prog: 100, fl: '—' },
];

const PILOT_STATS = [
  { icon: 'timer',        label: 'Flight Hours',  value: '1,284', sub: 'h logged' },
  { icon: 'plane',        label: 'Flights',       value: '612',   sub: 'legs completed' },
  { icon: 'arrow-down-to-line', label: 'Avg Landing', value: '-142', unit: 'fpm', badge: 'Butter' },
  { icon: 'check-circle-2', label: 'On-Time',     value: '96%',   sub: 'OTP rate' },
];

const ACHIEVEMENTS = [
  { name: 'Long Haul Explorer', icon: 'globe-2', accent: 'crimson', desc: 'Completed a 10,000 km+ leg — BEG → JFK.' },
  { name: 'Cross the Border',   icon: 'flag',    accent: 'royal',   desc: 'Flew 20+ regional Balkan sectors.' },
  { name: 'Precision Landing',  icon: 'target',  accent: 'emerald', desc: 'Greased a touchdown under -100 fpm.' },
  { name: 'Storm Chaser',       icon: 'wind',    locked: true, pct: 40, desc: 'Land in a 25 kt+ crosswind.' },
];

const TYPE_RATINGS = [
  { ac: 'ATR 72-600', code: 'AT76', hrs: '410h' },
  { ac: 'Airbus A319', code: 'A319', hrs: '288h' },
  { ac: 'Airbus A320', code: 'A320', hrs: '372h' },
  { ac: 'Airbus A330-200', code: 'A332', hrs: '214h' },
];

const LOGBOOK = [
  { no: 'ASV902', route: 'BEG → JFK', ac: 'A330-200', time: '09:42', land: -138 },
  { no: 'ASV451', route: 'CDG → BEG', ac: 'A320',     time: '02:18', land: -154 },
  { no: 'ASV118', route: 'BEG → IST', ac: 'A319',     time: '01:31', land: -119 },
  { no: 'ASV220', route: 'FRA → BEG', ac: 'A319',     time: '01:48', land: -167 },
  { no: 'ASV330', route: 'BEG → TIV', ac: 'ATR 72',   time: '01:05', land: -98 },
];

const FLEET = [
  { name: 'Airbus A319', code: 'A319 · YU-APA', img: 'assets/a319.png', cls: 'Narrow-body', range: '3,700 km', seats: '144', speed: 'M0.78', tag: 'Short / Medium haul',
    desc: 'The workhorse of the European network — efficient on thinner regional routes.' },
  { name: 'Airbus A320', code: 'A320 · YU-APB', img: 'assets/a319.png', cls: 'Narrow-body', range: '4,300 km', seats: '174', speed: 'M0.78', tag: 'Medium haul',
    desc: 'Higher-capacity single-aisle for trunk routes to major European hubs.' },
  { name: 'Airbus A330-200', code: 'A332 · YU-ARB', img: 'assets/a330.png', cls: 'Wide-body', range: '13,400 km', seats: '254', speed: 'M0.82', tag: 'Long haul · flagship',
    desc: 'The transatlantic flagship "Nikola Tesla" — Belgrade to New York non-stop.' },
  { name: 'ATR 72-600', code: 'AT76 · YU-ALN', img: 'assets/atr72.jpg', cls: 'Turboprop', range: '1,500 km', seats: '70', speed: '276 kt', tag: 'Regional · cadet start',
    desc: 'Twin turboprop for short regional hops — where every new cadet earns their first hours.' },
];

/* Airports for dispatch (distance in km from a simple great-circle estimate, cruise speed kt) */
const AIRPORTS = {
  LYBE: { city: 'Belgrade',    name: 'Nikola Tesla',    iata: 'BEG' },
  LFPG: { city: 'Paris',       name: 'Charles de Gaulle', iata: 'CDG' },
  EDDF: { city: 'Frankfurt',   name: 'Frankfurt Main',  iata: 'FRA' },
  LTFM: { city: 'Istanbul',    name: 'Istanbul',        iata: 'IST' },
  LGAV: { city: 'Athens',      name: 'Eleftherios Venizelos', iata: 'ATH' },
  LIRF: { city: 'Rome',        name: 'Fiumicino',       iata: 'FCO' },
  LSZH: { city: 'Zürich',      name: 'Zürich',          iata: 'ZRH' },
  EGLL: { city: 'London',      name: 'Heathrow',        iata: 'LHR' },
  KJFK: { city: 'New York',    name: 'John F. Kennedy', iata: 'JFK' },
  LYTV: { city: 'Tivat',       name: 'Tivat',           iata: 'TIV' },
  LYPG: { city: 'Podgorica',   name: 'Podgorica',       iata: 'TGD' },
  LROP: { city: 'Bucharest',   name: 'Henri Coandă',    iata: 'OTP' },
};
/* approx distances (km) from LYBE */
const DIST = { LFPG: 1450, EDDF: 1090, LTFM: 800, LGAV: 800, LIRF: 720, LSZH: 1010, EGLL: 1690, KJFK: 7470, LYTV: 360, LYPG: 330, LROP: 450, LYBE: 0 };

/* ===================== RENDER ===================== */
const fmtN = n => n.toLocaleString('en-US');

function renderStats() {
  const wrap = document.getElementById('statGrid');
  wrap.innerHTML = STATS.map(s => {
    const ring = s.accent === 'crimson' ? 'from-crimson/25 to-crimson/5 text-crimson-bright' : 'from-royal/25 to-royal/5 text-royal-bright';
    return `<div class="panel rounded-2xl p-5 relative overflow-hidden">
      <div class="absolute right-3 top-3 w-9 h-9 rounded-xl bg-gradient-to-br ${ring} grid place-items-center">
        <i data-lucide="${s.icon}" class="w-4 h-4"></i>
      </div>
      <div class="text-[11px] uppercase tracking-wider text-mist">${s.label}</div>
      <div class="stat-num font-display font-extrabold text-3xl sm:text-4xl mt-2" data-count="${s.value}">0</div>
      <div class="text-xs text-mist mt-1">${s.sub}</div>
    </div>`;
  }).join('');
}

function animateCounts() {
  document.querySelectorAll('[data-count]').forEach(el => {
    const target = +el.dataset.count;
    const dur = 1100; const start = performance.now();
    function step(now) {
      const p = Math.min((now - start) / dur, 1);
      const e = 1 - Math.pow(1 - p, 3);
      el.textContent = fmtN(Math.round(target * e));
      if (p < 1) requestAnimationFrame(step);
    }
    requestAnimationFrame(step);
  });
}

function renderRoutes() {
  document.getElementById('routeBars').innerHTML = ROUTES.map(r => `
    <div>
      <div class="flex items-center justify-between text-sm mb-1.5">
        <span class="font-mono font-semibold">${r.pair}</span>
        <span class="text-mist text-xs">${r.name}</span>
      </div>
      <div class="h-2.5 rounded-full bg-white/8 overflow-hidden">
        <div class="h-full rounded-full bg-gradient-to-r from-royal to-royal-bright" style="width:${r.pct}%"></div>
      </div>
    </div>`).join('');
}

function statusBadge(st) {
  const s = STATUS[st];
  return `<span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-semibold border whitespace-nowrap ${s.cls}">
    <span class="w-1.5 h-1.5 rounded-full ${s.dot} ${st==='enroute'?'dot-live':''}"></span>${s.label}</span>`;
}

function renderLiveCards() {
  const live = FLIGHTS.filter(f => f.st === 'enroute');
  document.getElementById('liveCount').textContent = live.length;
  document.getElementById('liveCards').innerHTML = live.map(f => `
    <div class="panel rounded-2xl p-5">
      <div class="flex items-center justify-between mb-4">
        <span class="font-mono font-bold text-lg">${f.no}</span>
        ${statusBadge(f.st)}
      </div>
      <div class="flex items-center justify-between mb-3">
        <div class="text-center"><div class="font-display font-extrabold text-2xl">${f.dep}</div><div class="text-[11px] text-mist">${f.depCity}</div></div>
        <div class="flex-1 px-3">
          <div class="relative h-px bg-white/15 my-3">
            <div class="absolute inset-y-0 left-0 bg-royal-bright" style="width:${f.prog}%;height:1.5px;top:-0.25px"></div>
            <span class="absolute -top-[7px]" style="left:calc(${f.prog}% - 7px)"><i data-lucide="plane" class="w-3.5 h-3.5 text-royal-bright"></i></span>
          </div>
        </div>
        <div class="text-center"><div class="font-display font-extrabold text-2xl">${f.arr}</div><div class="text-[11px] text-mist">${f.arrCity}</div></div>
      </div>
      <div class="flex items-center justify-between text-xs text-mist font-mono pt-3 border-t hairline">
        <span>${f.ac}</span><span>FL${f.fl}</span><span>${f.pilot}</span>
      </div>
    </div>`).join('');
}

function renderFlightRows() {
  document.getElementById('flightRows').innerHTML = FLIGHTS.map(f => `
    <tr class="hover:bg-white/[0.03] transition">
      <td class="px-6 py-3.5 font-mono font-bold">${f.no}</td>
      <td class="px-4 py-3.5"><span class="font-semibold">${f.dep} <span class="text-mist">→</span> ${f.arr}</span><div class="text-[11px] text-mist">${f.depCity} – ${f.arrCity}</div></td>
      <td class="px-4 py-3.5 font-mono text-mist">${f.ac}</td>
      <td class="px-4 py-3.5">${f.pilot}</td>
      <td class="px-4 py-3.5 font-mono text-mist">${f.dt}</td>
      <td class="px-6 py-3.5 text-right">${statusBadge(f.st)}</td>
    </tr>`).join('');
}

function renderPilotStats() {
  document.getElementById('pilotStats').innerHTML = PILOT_STATS.map(s => {
    const value = s.unit
      ? `<div class="flex items-baseline gap-1.5"><span class="font-display font-extrabold text-2xl stat-num">${s.value}</span><span class="text-xs font-mono text-mist">${s.unit}</span></div>`
      : `<div class="font-display font-extrabold text-2xl stat-num">${s.value}</div>`;
    const foot = s.badge
      ? `<span class="inline-flex items-center gap-1.5 mt-2 px-2.5 py-1 rounded-full text-[10px] font-bold tracking-wide bg-emerald-400/15 border border-emerald-400/40 text-emerald-300 net-glow"><span class="w-1.5 h-1.5 rounded-full bg-emerald-400"></span>${s.badge.toUpperCase()}</span>`
      : `<div class="text-xs text-mist mt-0.5">${s.sub}</div>`;
    return `<div class="panel rounded-2xl p-5">
      <div class="w-9 h-9 rounded-xl bg-gradient-to-br from-royal/25 to-royal/5 text-royal-bright grid place-items-center mb-3">
        <i data-lucide="${s.icon}" class="w-4 h-4"></i>
      </div>
      ${value}
      <div class="text-[11px] uppercase tracking-wider text-mist mt-1">${s.label}</div>
      ${foot}
    </div>`;
  }).join('');
}

const ACH_ACCENT = {
  crimson: { icon: 'from-crimson/30 to-crimson/5 text-crimson-bright', border: 'border-crimson/35', glow: '0 0 22px rgba(216,32,63,0.22)' },
  royal:   { icon: 'from-royal/30 to-royal/5 text-royal-bright',       border: 'border-royal/35',   glow: '0 0 22px rgba(63,134,244,0.22)' },
  emerald: { icon: 'from-emerald-400/30 to-emerald-400/5 text-emerald-300', border: 'border-emerald-400/35', glow: '0 0 22px rgba(52,211,153,0.20)' },
};

function renderAchievements() {
  document.getElementById('achievements').innerHTML = ACHIEVEMENTS.map(a => {
    if (a.locked) {
      return `<div class="rounded-2xl p-5 border hairline bg-ink-900/30">
        <div class="w-12 h-12 rounded-2xl grid place-items-center mb-3 bg-white/[0.04] border hairline text-mist/50">
          <i data-lucide="${a.icon}" class="w-6 h-6"></i>
        </div>
        <div class="font-display font-bold text-sm text-mist">${a.name}</div>
        <div class="text-[11px] text-mist/60 mt-1 leading-snug">${a.desc}</div>
        <div class="mt-3">
          <div class="flex items-center justify-between text-[10px] font-mono text-mist mb-1">
            <span class="flex items-center gap-1"><i data-lucide="lock" class="w-3 h-3"></i> LOCKED</span><span>${a.pct}%</span>
          </div>
          <div class="h-1.5 rounded-full bg-white/10 overflow-hidden"><div class="h-full rounded-full bg-mist/40" style="width:${a.pct}%"></div></div>
        </div>
      </div>`;
    }
    const c = ACH_ACCENT[a.accent];
    return `<div class="rounded-2xl p-5 border ${c.border} bg-ink-900/40" style="box-shadow:${c.glow}">
      <div class="w-12 h-12 rounded-2xl grid place-items-center mb-3 bg-gradient-to-br ${c.icon} border border-white/10">
        <i data-lucide="${a.icon}" class="w-6 h-6"></i>
      </div>
      <div class="font-display font-bold text-sm">${a.name}</div>
      <div class="text-[11px] text-mist mt-1 leading-snug">${a.desc}</div>
      <div class="mt-3 inline-flex items-center gap-1.5 text-[10px] font-mono font-bold text-emerald-300"><i data-lucide="check-circle-2" class="w-3 h-3"></i> UNLOCKED</div>
    </div>`;
  }).join('');
}

function renderTypeRatings() {
  document.getElementById('typeRatings').innerHTML = TYPE_RATINGS.map(t => `
    <div class="flex items-center gap-3 pl-3 pr-4 py-2.5 rounded-xl bg-ink-900/50 border hairline">
      <span class="font-mono text-[11px] font-bold px-2 py-1 rounded-md bg-royal/20 text-royal-bright">${t.code}</span>
      <div class="min-w-0"><div class="font-semibold text-sm whitespace-nowrap">${t.ac}</div><div class="text-[11px] text-mist font-mono whitespace-nowrap">${t.hrs} on type</div></div>
    </div>`).join('');
}

function landTag(fpm) {
  let cls = 'text-emerald-300', word = 'Smooth';
  if (fpm < -200) { cls = 'text-amber-300'; word = 'Firm'; }
  if (fpm < -350) { cls = 'text-crimson-bright'; word = 'Hard'; }
  return `<span class="font-mono font-semibold ${cls}">${fpm} fpm</span> <span class="text-[10px] text-mist">${word}</span>`;
}

function renderLogbook() {
  document.getElementById('logbookRows').innerHTML = LOGBOOK.map(l => `
    <tr class="hover:bg-white/[0.03] transition">
      <td class="px-6 py-3.5 font-mono font-bold">${l.no}</td>
      <td class="px-4 py-3.5 font-semibold">${l.route}</td>
      <td class="px-4 py-3.5 font-mono text-mist">${l.ac}</td>
      <td class="px-4 py-3.5 font-mono text-mist">${l.time}</td>
      <td class="px-6 py-3.5 text-right">${landTag(l.land)}</td>
    </tr>`).join('');
}

function renderFleet() {
  document.getElementById('fleetGrid').innerHTML = FLEET.map(a => `
    <div class="panel rounded-2xl overflow-hidden group">
      <div class="relative h-52 overflow-hidden">
        <img src="${a.img}" alt="${a.name}" class="w-full h-full object-cover transition duration-700 group-hover:scale-105" />
        <div class="absolute inset-0" style="background:linear-gradient(0deg,#0c2244 4%,transparent 55%)"></div>
        <span class="absolute top-4 left-4 px-2.5 py-1 rounded-full bg-ink-900/70 backdrop-blur border hairline text-[11px] font-mono whitespace-nowrap">${a.cls}</span>
        <span class="absolute top-4 right-4 px-2.5 py-1 rounded-full bg-crimson/85 text-[11px] font-semibold whitespace-nowrap">${a.tag}</span>
      </div>
      <div class="p-6">
        <div class="flex items-start justify-between">
          <div>
            <h3 class="font-display font-bold text-xl">${a.name}</h3>
            <div class="font-mono text-xs text-mist mt-1">${a.code}</div>
          </div>
        </div>
        <p class="text-sm text-slate-300/85 mt-3 leading-relaxed">${a.desc}</p>
        <div class="grid grid-cols-3 gap-3 mt-5">
          <div class="rounded-xl bg-ink-900/50 border hairline p-3"><div class="text-[10px] uppercase tracking-wider text-mist">Range</div><div class="font-mono font-bold text-sm mt-1">${a.range}</div></div>
          <div class="rounded-xl bg-ink-900/50 border hairline p-3"><div class="text-[10px] uppercase tracking-wider text-mist">Seats</div><div class="font-mono font-bold text-sm mt-1">${a.seats}</div></div>
          <div class="rounded-xl bg-ink-900/50 border hairline p-3"><div class="text-[10px] uppercase tracking-wider text-mist">Speed</div><div class="font-mono font-bold text-sm mt-1">${a.speed}</div></div>
        </div>
        <div class="grid grid-cols-2 gap-3 mt-5">
          <button class="dl flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-royal hover:bg-royal-bright text-white text-sm font-bold transition whitespace-nowrap">
            <i data-lucide="download" class="w-4 h-4"></i> Download MSFS Livery
          </button>
          <button class="dl flex items-center justify-center gap-2 px-4 py-3 rounded-xl panel hover:border-white/30 text-white text-sm font-bold transition whitespace-nowrap">
            <i data-lucide="download" class="w-4 h-4"></i> Download X-Plane Livery
          </button>
        </div>
      </div>
    </div>`).join('');
}

/* ---- Downloads & Resources ---- */
const LIVERY_GROUPS = [
  { sim: 'MSFS 2024',  icon: 'monitor', items: ['Airbus A320-200', 'ATR 72-600', 'Airbus A330-200'] },
  { sim: 'X-Plane 12', icon: 'monitor', items: ['Airbus A320-200', 'ATR 72-600', 'Airbus A330-200'] },
];
const DOCS = [
  { name: 'ASV Pilot Handbook', meta: 'PDF · 4.2 MB', icon: 'book-open' },
  { name: 'Standard Operating Procedures (SOP)', meta: 'PDF · 2.1 MB', icon: 'clipboard-list' },
  { name: 'Livery Installation Guide', meta: 'PDF · 1.3 MB', icon: 'file-text' },
];
const CHARTS = [
  { name: 'LYBE — Belgrade', meta: 'Nikola Tesla · airport charts', icon: 'map' },
  { name: 'LYNI — Niš', meta: 'Constantine the Great · charts', icon: 'map' },
  { name: 'Airbus FCOM', meta: 'A319 / A320 / A330 · OEM', icon: 'book-open' },
  { name: 'ATR 72-600 FCOM', meta: 'OEM flight manual', icon: 'book-open' },
];

function renderDownloads() {
  document.getElementById('liveryGroups').innerHTML = LIVERY_GROUPS.map(g => `
    <div class="rounded-xl bg-ink-900/40 border hairline p-4">
      <div class="flex items-center gap-2 mb-3"><i data-lucide="${g.icon}" class="w-4 h-4 text-mist"></i><span class="font-mono text-[11px] tracking-widest text-mist">${g.sim.toUpperCase()}</span></div>
      <div class="space-y-2">
        ${g.items.map(it => `<button class="dl w-full flex items-center justify-between gap-2 px-3 py-2.5 rounded-lg bg-white/[0.03] hover:bg-white/[0.07] border hairline transition text-left"><span class="text-sm font-semibold">${it}</span><i data-lucide="download" class="w-4 h-4 text-royal-bright"></i></button>`).join('')}
      </div>
    </div>`).join('');

  document.getElementById('docList').innerHTML = DOCS.map(d => `
    <div class="flex items-center gap-3 px-3.5 py-3 rounded-xl bg-ink-900/40 border hairline">
      <span class="w-9 h-9 rounded-lg bg-crimson/15 text-crimson-bright grid place-items-center shrink-0"><i data-lucide="${d.icon}" class="w-4 h-4"></i></span>
      <div class="min-w-0 flex-1"><div class="font-semibold text-sm truncate">${d.name}</div><div class="text-[11px] text-mist font-mono">${d.meta}</div></div>
      <button class="dl shrink-0 w-9 h-9 grid place-items-center rounded-lg bg-white/[0.04] hover:bg-white/[0.1] border hairline transition"><i data-lucide="download" class="w-4 h-4 text-mist"></i></button>
    </div>`).join('');

  document.getElementById('chartList').innerHTML = CHARTS.map(c => `
    <a href="#" class="flex items-center gap-3 px-4 py-3.5 rounded-xl bg-ink-900/40 border hairline hover:border-white/25 transition group">
      <span class="w-9 h-9 rounded-lg bg-royal/15 text-royal-bright grid place-items-center shrink-0"><i data-lucide="${c.icon}" class="w-4 h-4"></i></span>
      <div class="min-w-0 flex-1"><div class="font-semibold text-sm truncate">${c.name}</div><div class="text-[11px] text-mist truncate">${c.meta}</div></div>
      <i data-lucide="external-link" class="w-4 h-4 text-mist group-hover:text-white transition shrink-0"></i>
    </a>`).join('');
}

/* ---- Heritage timeline ---- */
const ERAS = [
  { id: 'jat', years: '1927 – 2003', name: 'JAT — Jugoslovenski Aerotransport', short: 'JAT', tag: 'The golden age of flying',
    accent: 'amber', icon: 'plane',
    desc: 'Founded in 1927, JAT Yugoslav Airlines grew into one of Europe\'s most respected flag carriers. At its peak it reached five continents, flying the iconic Douglas DC-10 and Boeing 727 through the golden jet age — a symbol of national pride and global reach.',
    aircraft: ['Douglas DC-10', 'Boeing 727', 'Boeing 737-300', 'Caravelle'], livery: 'JAT classic blue & white' },
  { id: 'jatw', years: '2003 – 2013', name: 'Jat Airways', short: 'Jat Airways', tag: 'The silver-blue transition',
    accent: 'slate', icon: 'plane',
    desc: 'After 2003 the carrier rebranded as Jat Airways, adopting a clean silver-blue identity. The fleet consolidated around dependable regional workhorses — the Boeing 737-300 and ATR 72-200 — serving Europe and the Mediterranean through a period of transition.',
    aircraft: ['Boeing 737-300', 'ATR 72-200'], livery: 'Jat Airways silver-blue' },
  { id: 'asv', years: '2013 – Present', name: 'Air Serbia', short: 'Air Serbia', tag: 'The modern era',
    accent: 'royal', icon: 'plane',
    desc: 'In 2013 a partnership with Etihad Airways relaunched the carrier as Air Serbia. Fleet modernization brought the Airbus A319, A320 and the A330-200 flagship "Nikola Tesla" — reopening long-haul routes and expanding the network across Europe, the Middle East and North America.',
    aircraft: ['Airbus A319', 'Airbus A320', 'Airbus A330-200', 'ATR 72-600'], livery: 'Air Serbia modern' },
];
const ERA_ACC = {
  amber: { text: 'text-amber-300', node: 'bg-amber-400/15 text-amber-300 border-amber-400/40', ring: 'rgba(251,191,36,0.5)', glow: 'rgba(251,191,36,0.16)' },
  slate: { text: 'text-slate-300', node: 'bg-slate-400/15 text-slate-200 border-slate-400/40', ring: 'rgba(148,163,184,0.5)', glow: 'rgba(148,163,184,0.16)' },
  royal: { text: 'text-royal-bright', node: 'bg-royal/20 text-royal-bright border-royal/45', ring: 'rgba(63,134,244,0.55)', glow: 'rgba(63,134,244,0.18)' },
};

function renderTimeline() {
  document.getElementById('timeline').innerHTML = ERAS.map((e, i) => {
    const c = ERA_ACC[e.accent];
    const open = i === ERAS.length - 1;
    return `<div class="relative era" data-era="${e.id}">
      <span class="absolute -left-[42px] sm:-left-[58px] top-4 w-9 h-9 rounded-full grid place-items-center border ${c.node}"><i data-lucide="${e.icon}" class="w-4 h-4"></i></span>
      <div class="era-card rounded-2xl border bg-ink-900/40 overflow-hidden ${open ? 'era-active' : ''}" style="--era-ring:${c.ring};--era-glow:${c.glow}">
        <button class="era-head w-full flex items-center justify-between gap-3 px-5 py-4 text-left">
          <div class="min-w-0">
            <div class="font-mono text-[11px] tracking-widest ${c.text}">${e.years}</div>
            <div class="font-display font-bold text-lg mt-0.5">${e.name}</div>
            <div class="text-xs text-mist mt-0.5">${e.tag}</div>
          </div>
          <i data-lucide="chevron-down" class="era-chevron w-5 h-5 text-mist shrink-0 transition ${open ? 'rotate-180' : ''}"></i>
        </button>
        <div class="era-body px-5 pb-5 ${open ? '' : 'hidden'}">
          <p class="text-sm text-slate-300/90 leading-relaxed">${e.desc}</p>
          <div class="flex flex-wrap gap-2 mt-4">${e.aircraft.map(a => `<span class="px-2.5 py-1 rounded-lg bg-white/[0.04] border hairline text-[11px] font-mono text-mist">${a}</span>`).join('')}</div>
          <div class="mt-4 rounded-xl border hairline overflow-hidden">
            <div class="h-20 grid place-items-center text-[10px] font-mono tracking-widest text-mist/70" style="background:repeating-linear-gradient(45deg, rgba(255,255,255,0.045) 0 10px, transparent 10px 20px)">${e.livery.toUpperCase()} — LIVERY PREVIEW</div>
            <button class="dl w-full flex items-center justify-center gap-2 px-4 py-3 text-sm font-bold bg-white/[0.03] hover:bg-white/[0.07] transition"><i data-lucide="download" class="w-4 h-4 ${c.text}"></i> Download ${e.short} retro livery</button>
          </div>
        </div>
      </div>
    </div>`;
  }).join('');
}

function setupTimeline() {
  const tl = document.getElementById('timeline');
  tl.addEventListener('click', ev => {
    const head = ev.target.closest('.era-head');
    if (!head) return;
    const era = head.closest('.era');
    const body = era.querySelector('.era-body');
    const willOpen = body.classList.contains('hidden');
    tl.querySelectorAll('.era').forEach(x => {
      x.querySelector('.era-body').classList.add('hidden');
      x.querySelector('.era-chevron').classList.remove('rotate-180');
      x.querySelector('.era-card').classList.remove('era-active');
    });
    if (willOpen) {
      body.classList.remove('hidden');
      era.querySelector('.era-chevron').classList.add('rotate-180');
      era.querySelector('.era-card').classList.add('era-active');
    }
  });
}

/* ---- Schedules ---- */
let forcedFlightNo = null;
let schedPage = 1;

function rankFor(name) { return name.includes('ATR') ? 'Cadet' : name.includes('330') ? 'Captain' : 'First Officer'; }
function acByDist(km) {
  if (km < 500)  return { name: 'ATR 72-600', sel: 'ATR 72-600 (AT76)', kt: 276 };
  if (km < 1200) return { name: 'A319', sel: 'Airbus A319 (A319)', kt: 458 };
  if (km < 2600) return { name: 'A320', sel: 'Airbus A320 (A320)', kt: 458 };
  return { name: 'A330-200', sel: 'Airbus A330-200 (A332)', kt: 458 };
}
function zulu(min) { min = ((min % 1440) + 1440) % 1440; return String(Math.floor(min / 60)).padStart(2,'0') + String(min % 60).padStart(2,'0') + 'z'; }
function durStr(min) { return Math.floor(min / 60) + ':' + String(min % 60).padStart(2,'0'); }

function buildSchedules() {
  const dests = Object.keys(AIRPORTS).filter(k => k !== 'LYBE');
  const opSets = ['Daily', 'Mo · We · Fr', 'Tu · Th · Sa', 'Mo – Fr', 'We · Sa · Su', 'Daily', 'Mo · Th · Su'];
  const rows = []; let n = 100;
  dests.forEach((d, i) => {
    const km = Math.max(DIST[d] || 500, 220);
    const ac = acByDist(km);
    const durMin = Math.round((km / 1.852) / ac.kt * 60) + 25;
    const depMin = ((6 + i) % 18) * 60 + (i * 7 % 60);
    rows.push({ no: 'ASV' + (n++), depI: 'LYBE', arrI: d, dep: AIRPORTS.LYBE.iata, arr: AIRPORTS[d].iata, depMin, arrMin: depMin + durMin, durMin, ac: ac.name, acSel: ac.sel, opDays: opSets[i % opSets.length], rank: rankFor(ac.name) });
    const depMin2 = (depMin + durMin + 75);
    rows.push({ no: 'ASV' + (n++), depI: d, arrI: 'LYBE', dep: AIRPORTS[d].iata, arr: AIRPORTS.LYBE.iata, depMin: depMin2 % 1440, arrMin: depMin2 % 1440 + durMin, durMin, ac: ac.name, acSel: ac.sel, opDays: opSets[(i + 3) % opSets.length], rank: rankFor(ac.name) });
  });
  return rows;
}
const SCHEDULES = buildSchedules();

const RANK_PILL = {
  'Cadet': 'bg-emerald-400/15 text-emerald-300',
  'First Officer': 'bg-royal/20 text-royal-bright',
  'Captain': 'bg-crimson/20 text-crimson-bright',
};

function getSchedFilters() {
  const v = id => document.getElementById(id).value;
  return {
    flight: v('fFlight').trim().toLowerCase(),
    dep: v('fDep').trim().toLowerCase(),
    arr: v('fArr').trim().toLowerCase(),
    ac: v('fAc'), dur: v('fDur'), rank: v('fRank'), order: v('fOrder'),
  };
}

function renderSchedules() {
  const f = getSchedFilters();
  let rows = SCHEDULES.filter(r => {
    if (f.flight && !r.no.toLowerCase().includes(f.flight)) return false;
    if (f.dep && !(r.dep.toLowerCase().includes(f.dep) || r.depI.toLowerCase().includes(f.dep))) return false;
    if (f.arr && !(r.arr.toLowerCase().includes(f.arr) || r.arrI.toLowerCase().includes(f.arr))) return false;
    if (f.ac && f.ac !== 'any' && r.acSel !== f.ac) return false;
    if (f.dur === 'short' && !(r.durMin < 120)) return false;
    if (f.dur === 'med' && !(r.durMin >= 120 && r.durMin < 300)) return false;
    if (f.dur === 'long' && !(r.durMin >= 300)) return false;
    if (f.rank !== 'any' && r.rank !== f.rank) return false;
    return true;
  });
  if (f.order === 'flight') rows.sort((a, b) => a.no.localeCompare(b.no));
  else if (f.order === 'durAsc') rows.sort((a, b) => a.durMin - b.durMin);
  else if (f.order === 'durDesc') rows.sort((a, b) => b.durMin - a.durMin);
  else if (f.order === 'dep') rows.sort((a, b) => a.dep.localeCompare(b.dep) || a.no.localeCompare(b.no));

  const total = rows.length, pageSize = 9, pages = Math.max(1, Math.ceil(total / pageSize));
  if (schedPage > pages) schedPage = 1;
  const start = (schedPage - 1) * pageSize;
  const view = rows.slice(start, start + pageSize);

  document.getElementById('schedRows').innerHTML = view.length ? view.map(r => `
    <tr class="hover:bg-white/[0.03] transition">
      <td class="px-5 py-3 font-mono font-bold">${r.no}</td>
      <td class="px-3 py-3 font-mono">${r.dep}</td>
      <td class="px-3 py-3 font-mono">${r.arr}</td>
      <td class="px-3 py-3 font-mono text-mist">${zulu(r.depMin)}</td>
      <td class="px-3 py-3 font-mono text-mist">${zulu(r.arrMin)}</td>
      <td class="px-3 py-3 font-mono">${durStr(r.durMin)}</td>
      <td class="px-3 py-3"><span class="font-mono text-[11px] px-2 py-0.5 rounded ${RANK_PILL[r.rank]}">${r.ac}</span></td>
      <td class="px-3 py-3 text-mist text-xs whitespace-nowrap">${r.opDays}</td>
      <td class="px-5 py-3 text-right"><button class="sched-book inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-crimson hover:bg-crimson-bright text-white text-xs font-bold transition whitespace-nowrap" data-dep="${r.depI}" data-arr="${r.arrI}" data-ac="${r.acSel}" data-no="${r.no}"><i data-lucide="send" class="w-3.5 h-3.5"></i> Book</button></td>
    </tr>`).join('') : `<tr><td colspan="9" class="px-5 py-14 text-center text-mist">No flights match your filters.</td></tr>`;

  document.getElementById('schedResult').textContent = total ? `Showing ${start + 1}–${Math.min(start + pageSize, total)} of ${total} routes` : '0 routes match';
  document.getElementById('schedCount').textContent = SCHEDULES.length + ' routes';

  const pager = document.getElementById('schedPager');
  if (pages <= 1) { pager.innerHTML = ''; }
  else {
    let h = `<button class="sched-pg w-8 h-8 grid place-items-center rounded-lg panel text-mist hover:text-white transition ${schedPage === 1 ? 'opacity-40 pointer-events-none' : ''}" data-pg="${schedPage - 1}"><i data-lucide="chevron-left" class="w-4 h-4"></i></button>`;
    for (let p = 1; p <= pages; p++) h += `<button class="sched-pg w-8 h-8 grid place-items-center rounded-lg text-sm font-semibold transition ${p === schedPage ? 'bg-royal text-white' : 'panel text-mist hover:text-white'}" data-pg="${p}">${p}</button>`;
    h += `<button class="sched-pg w-8 h-8 grid place-items-center rounded-lg panel text-mist hover:text-white transition ${schedPage === pages ? 'opacity-40 pointer-events-none' : ''}" data-pg="${schedPage + 1}"><i data-lucide="chevron-right" class="w-4 h-4"></i></button>`;
    pager.innerHTML = h;
  }
  lucide.createIcons();
}

function bookFlight(dep, arr, acSel, no) {
  document.getElementById('depSel').value = dep;
  document.getElementById('arrSel').value = arr;
  document.getElementById('acSel').value = acSel;
  forcedFlightNo = no;
  setTab('dispatch');
  genPlan();
  toast('Booked ' + no + ' — OFP generated');
}

function setupSchedules() {
  document.getElementById('fAc').innerHTML = `<option value="any">Any aircraft</option>` +
    ['ATR 72-600 (AT76)', 'Airbus A319 (A319)', 'Airbus A320 (A320)', 'Airbus A330-200 (A332)'].map(s => `<option value="${s}">${s}</option>`).join('');
  ['fFlight', 'fDep', 'fArr'].forEach(id => document.getElementById(id).addEventListener('input', () => { schedPage = 1; renderSchedules(); }));
  ['fAc', 'fDur', 'fRank', 'fOrder'].forEach(id => document.getElementById(id).addEventListener('change', () => { schedPage = 1; renderSchedules(); }));
  document.getElementById('fReset').addEventListener('click', () => {
    ['fFlight', 'fDep', 'fArr'].forEach(id => document.getElementById(id).value = '');
    document.getElementById('fAc').value = 'any'; document.getElementById('fDur').value = 'any';
    document.getElementById('fRank').value = 'any'; document.getElementById('fOrder').value = 'flight';
    schedPage = 1; renderSchedules();
  });
  document.getElementById('schedPager').addEventListener('click', e => {
    const b = e.target.closest('.sched-pg'); if (!b) return;
    schedPage = +b.dataset.pg; renderSchedules();
  });
  document.getElementById('schedRows').addEventListener('click', e => {
    const b = e.target.closest('.sched-book'); if (!b) return;
    bookFlight(b.dataset.dep, b.dataset.arr, b.dataset.ac, b.dataset.no);
  });
}

/* ---- Dispatch ---- */
function fillSelects() {
  const dep = document.getElementById('depSel');
  const arr = document.getElementById('arrSel');
  const opts = Object.entries(AIRPORTS).map(([icao, a]) =>
    `<option value="${icao}">${icao} · ${a.iata} — ${a.city} (${a.name})</option>`).join('');
  dep.innerHTML = opts; arr.innerHTML = opts;
  dep.value = 'LYBE'; arr.value = 'LFPG';
  document.getElementById('acSel').innerHTML =
    ['ATR 72-600 (AT76)', 'Airbus A319 (A319)', 'Airbus A320 (A320)', 'Airbus A330-200 (A332)']
      .map(o => `<option>${o}</option>`).join('');
  document.getElementById('acSel').value = 'Airbus A320 (A320)';
}

function genPlan() {
  const depI = document.getElementById('depSel').value;
  const arrI = document.getElementById('arrSel').value;
  const ac = document.getElementById('acSel').value;
  const body = document.getElementById('planBody');
  const tag = document.getElementById('planTag');

  if (depI === arrI) {
    tag.textContent = '— INVALID —';
    body.innerHTML = `<div class="h-full grid place-items-center text-center py-10">
      <div><i data-lucide="alert-triangle" class="w-8 h-8 text-amber-400 mx-auto mb-3"></i>
      <p class="text-mist">Departure and destination must differ.</p></div></div>`;
    lucide.createIcons(); return;
  }

  const dep = AIRPORTS[depI], arr = AIRPORTS[arrI];
  // distance: from BEG table, or sum via BEG as a rough router
  let dist;
  if (depI === 'LYBE') dist = DIST[arrI];
  else if (arrI === 'LYBE') dist = DIST[depI];
  else dist = (DIST[depI] || 0) + (DIST[arrI] || 0);
  dist = Math.max(dist, 220);

  const isProp = ac.includes('ATR');
  const isWide = ac.includes('A330');
  const cruiseKt = isProp ? 276 : 458;
  const distNm = Math.round(dist / 1.852);
  const eteH = distNm / cruiseKt;
  const hrs = Math.floor(eteH);
  const mins = Math.round((eteH - hrs) * 60);
  const fl = isProp ? (180 + Math.round(Math.random()) * 30) : (dist > 2500 ? 380 : dist > 900 ? 360 : 320);
  const burnPerNm = isProp ? 3.0 : isWide ? 9.5 : 5.2; // kg/nm rough
  const fuel = Math.round((distNm * burnPerNm + 1800) / 10) * 10;
  const flightNo = forcedFlightNo || ('ASV' + (100 + Math.floor(Math.random() * 899)));
  forcedFlightNo = null;
  const sid = ['VANEK1A', 'KLB2C', 'TISA3N', 'NISVA1B'][Math.floor(Math.random()*4)];
  const star = ['ROLLO2A', 'DEGOS1C', 'BABIT4', 'UNDET2A'][Math.floor(Math.random()*4)];
  const fixes = ['VBA', 'NEMUS', 'OKTAR', 'GIPNO', 'RIXED', 'BALAP', 'ELDAR', 'ROTAR'];
  const depRwy = (isProp ? ['12','30','17'] : ['22R','30L','12','30'])[Math.floor(Math.random()*(isProp?3:4))];
  const arrRwy = (['26L','09R','25','07','34L','16R'])[Math.floor(Math.random()*6)];
  const airways = ['DCT VAL', 'G42 NAKIT', 'UL604 ARTAT', 'UN871 GOLOB', 'DCT ROVOS'];
  const midRoute = airways.slice(0, isWide ? 4 : 3).join(' ');
  const route = `${sid} ${midRoute} ${star}`;
  const fullRoute = `${depI}/${depRwy} ${route} ${arrI}/${arrRwy}`;
  const alt = depI === 'LYBE' ? 'LYNI' : 'LYBE';
  const ci = isProp ? 18 : isWide ? 52 : 45;
  const payload = isProp ? (5800 + Math.floor(Math.random()*900)) : isWide ? (28400 + Math.floor(Math.random()*4000)) : (13600 + Math.floor(Math.random()*1600));
  const zfw = (isProp ? 18000 : isWide ? 124000 : 48000) + payload;
  const tow = zfw + fuel;
  const pax = isProp ? 70 : isWide ? 254 : 174;
  const acType = ac.match(/\(([^)]+)\)/) ? ac.match(/\(([^)]+)\)/)[1] : ac;
  const utc = new Date().toISOString().slice(11,16);

  tag.textContent = `OFP · ${flightNo}`;
  body.innerHTML = `
    <div class="flex items-center justify-between pb-5 border-b hairline">
      <div class="flex items-center gap-4">
        <div class="text-center"><div class="font-display font-extrabold text-3xl">${dep.iata}</div><div class="text-[11px] text-mist">${dep.city}</div></div>
        <div class="px-3 text-mist"><i data-lucide="plane" class="w-5 h-5"></i></div>
        <div class="text-center"><div class="font-display font-extrabold text-3xl">${arr.iata}</div><div class="text-[11px] text-mist">${arr.city}</div></div>
      </div>
      <div class="text-right">
        <div class="font-mono font-bold text-xl text-crimson-bright">${flightNo}</div>
        <div class="text-[11px] text-mist mt-0.5">${ac}</div>
      </div>
    </div>
    <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 py-5">
      ${[['Distance', distNm.toLocaleString()+' nm'],['Est. Time', hrs+'h '+String(mins).padStart(2,'0')+'m'],['Cruise', 'FL'+fl],['Cost Index', 'CI '+ci]]
        .map(([k,v])=>`<div class="rounded-xl bg-ink-900/50 border hairline p-3.5"><div class="text-[10px] uppercase tracking-wider text-mist">${k}</div><div class="font-mono font-bold text-lg mt-1">${v}</div></div>`).join('')}
    </div>

    <!-- SimBrief-style briefing block -->
    <div class="rounded-xl bg-[#040c1c] border hairline overflow-hidden">
      <div class="flex items-center justify-between px-4 py-2.5 border-b hairline bg-ink-900/60">
        <span class="font-mono text-[11px] tracking-widest text-mist">// OPERATIONAL FLIGHT PLAN</span>
        <span class="font-mono text-[11px] text-mist">GEN ${utc}Z</span>
      </div>
      <pre class="px-4 py-4 font-mono text-[12px] leading-[1.7] text-slate-300 whitespace-pre-wrap break-words">FLIGHT  <span class="text-white font-bold">${flightNo}</span>   ACFT <span class="text-white">${acType}</span>   PAX <span class="text-white">${pax}</span>
ROUTE   <span class="text-royal-bright">${fullRoute}</span>
ALTN    <span class="text-white">${alt}</span>      CRZ <span class="text-white">${cruiseKt} KT ${isProp?'TAS':'M0.78'}</span>
─────────────────────────────────────────
BLOCK FUEL  <span class="text-white">${fuel.toLocaleString()} KG</span>      RESV <span class="text-white">1,800 KG</span>
PAYLOAD     <span class="text-white">${payload.toLocaleString()} KG</span>      ZFW  <span class="text-white">${zfw.toLocaleString()} KG</span>
EST T/O WT  <span class="text-white">${tow.toLocaleString()} KG</span>   COST IDX <span class="text-white">${ci}</span></pre>
    </div>

    <!-- release banner -->
    <div class="mt-4 flex items-center gap-3 rounded-xl px-4 py-3 bg-emerald-400/10 border border-emerald-400/35 net-glow">
      <span class="w-8 h-8 rounded-lg bg-emerald-400/20 grid place-items-center text-emerald-300"><i data-lucide="shield-check" class="w-4 h-4"></i></span>
      <div>
        <div class="font-mono text-[11px] tracking-widest text-mist">DISPATCH RELEASE STATUS</div>
        <div class="font-display font-bold text-emerald-300 text-sm">APPROVED BY ACARS · ${flightNo}</div>
      </div>
      <span class="ml-auto w-2 h-2 rounded-full bg-emerald-400 dot-on"></span>
    </div>

    <div class="flex flex-wrap gap-3 mt-5">
      <button class="dl flex items-center gap-2 px-4 py-3 rounded-xl bg-crimson hover:bg-crimson-bright text-white text-sm font-bold transition whitespace-nowrap"><i data-lucide="send" class="w-4 h-4"></i> Dispatch to ACARS</button>
      <button class="dl flex items-center gap-2 px-4 py-3 rounded-xl panel hover:border-white/30 text-white text-sm font-bold transition whitespace-nowrap"><i data-lucide="download" class="w-4 h-4"></i> Export .pln / .fms</button>
    </div>`;
  lucide.createIcons();
}

/* ---- toast for download/dispatch buttons ---- */
function toast(msg) {
  let t = document.getElementById('asv-toast');
  if (!t) {
    t = document.createElement('div');
    t.id = 'asv-toast';
    t.className = 'fixed bottom-6 left-1/2 -translate-x-1/2 z-50 px-5 py-3 rounded-xl panel-solid shadow-2xl text-sm font-semibold flex items-center gap-2 transition-all duration-300';
    t.style.opacity = '0'; t.style.transform = 'translate(-50%, 12px)';
    document.body.appendChild(t);
  }
  t.innerHTML = `<span class="w-2 h-2 rounded-full bg-emerald-400"></span> ${msg}`;
  requestAnimationFrame(() => { t.style.opacity = '1'; t.style.transform = 'translate(-50%, 0)'; });
  clearTimeout(t._t);
  t._t = setTimeout(() => { t.style.opacity = '0'; t.style.transform = 'translate(-50%, 12px)'; }, 2200);
}

/* ===================== INIT ===================== */
function init() {
  mountEmblems();
  renderStats();
  renderRoutes();
  renderLiveCards();
  renderFlightRows();
  renderPilotStats();
  renderTypeRatings();
  renderAchievements();
  renderLogbook();
  renderFleet();
  renderDownloads();
  renderTimeline();
  setupTimeline();
  setupSchedules();
  renderSchedules();
  fillSelects();

  lucide.createIcons();
  animateCounts();

  tickClock(); setInterval(tickClock, 1000);

  document.getElementById('genBtn').addEventListener('click', genPlan);
  document.getElementById('swapBtn').addEventListener('click', () => {
    const d = document.getElementById('depSel'), a = document.getElementById('arrSel');
    [d.value, a.value] = [a.value, d.value];
  });
  // download / dispatch toasts (delegated)
  document.addEventListener('click', e => {
    const b = e.target.closest('.dl');
    if (b) toast(b.textContent.trim() + ' — queued');
  });

  // re-run icons after any dynamic insert that needs them on first paint of other tabs
  lucide.createIcons();
}

if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
else init();
