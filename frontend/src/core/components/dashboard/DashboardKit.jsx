import "./dashboardKit.css";

// Dependency-free SVG chart kit. recharts is unusable under this project's
// rolldown-vite dep optimizer (es-toolkit CJS interop crash), so the charts
// are hand-rolled: donut arcs, bars and an area trend cover every dashboard.

// Theme-aligned categorical palette (navy/gold first, then semantic accents).
const DK_PALETTE = [
  "#2e3591",
  "#c9a84c",
  "#2563eb",
  "#16a34a",
  "#be185d",
  "#dc2626",
  "#0d9488",
  "#7c3aed",
  "#ea580c",
  "#64748b",
];

const DK_TONES = {
  navy: { background: "rgba(26,31,94,0.08)", color: "#1a1f5e" },
  gold: { background: "rgba(201,168,76,0.12)", color: "#7a5c10" },
  blue: { background: "rgba(96,165,250,0.12)", color: "#2563eb" },
  pink: { background: "rgba(244,114,182,0.12)", color: "#be185d" },
  green: { background: "rgba(22,163,74,0.1)", color: "#15803d" },
  red: { background: "rgba(220,38,38,0.08)", color: "#b91c1c" },
  teal: { background: "rgba(13,148,136,0.1)", color: "#0f766e" },
};

const defaultFormat = (v) => Number(v ?? 0).toLocaleString("en-US");

/** KPI tile. `icon` takes a component reference, not an element. */
export function StatCard({ icon: Icon, label, value, sub, subTone, tone = "navy", loading }) {
  return (
    <div className="dk-stat-card">
      <div className="dk-stat-top">
        <span className="dk-stat-label">{label}</span>
        {Icon && (
          <div className="dk-stat-icon" style={DK_TONES[tone] || DK_TONES.navy}>
            <Icon size={17} />
          </div>
        )}
      </div>
      {loading ? (
        <div className="dk-skeleton-line" />
      ) : (
        <h2 className="dk-stat-value">{value ?? "—"}</h2>
      )}
      {sub && !loading && <p className={`dk-stat-sub ${subTone || ""}`}>{sub}</p>}
    </div>
  );
}

/** Card shell for a chart/list widget with loading + empty states. */
export function ChartCard({ icon: Icon, title, subtitle, action, loading, empty, emptyLabel, height = 240, children }) {
  return (
    <div className="dk-chart-card">
      <div className="dk-chart-head">
        <h3 className="dk-chart-title">{title}</h3>
        {action || (Icon && <Icon size={17} />)}
      </div>
      {subtitle && <p className="dk-chart-subtitle">{subtitle}</p>}
      <div className="dk-chart-body">
        {loading ? (
          <div className="dk-skeleton" style={{ height }} />
        ) : empty ? (
          <div className="dk-chart-empty" style={{ height }}>{emptyLabel || "—"}</div>
        ) : (
          children
        )}
      </div>
    </div>
  );
}

function polar(cx, cy, r, angle) {
  const rad = ((angle - 90) * Math.PI) / 180;
  return [cx + r * Math.cos(rad), cy + r * Math.sin(rad)];
}

// Annular sector path between startAngle/endAngle (degrees, clockwise from 12).
function arcPath(cx, cy, rOuter, rInner, startAngle, endAngle) {
  const large = endAngle - startAngle > 180 ? 1 : 0;
  const [ox1, oy1] = polar(cx, cy, rOuter, startAngle);
  const [ox2, oy2] = polar(cx, cy, rOuter, endAngle);
  const [ix1, iy1] = polar(cx, cy, rInner, endAngle);
  const [ix2, iy2] = polar(cx, cy, rInner, startAngle);
  return [
    `M ${ox1} ${oy1}`,
    `A ${rOuter} ${rOuter} 0 ${large} 1 ${ox2} ${oy2}`,
    `L ${ix1} ${iy1}`,
    `A ${rInner} ${rInner} 0 ${large} 0 ${ix2} ${iy2}`,
    "Z",
  ].join(" ");
}

/**
 * Donut breakdown. data: [{ name, value, color? }]. Slices with value 0 are
 * dropped from the ring but kept in the legend.
 */
export function DonutChart({ data = [], height = 190, centerLabel, centerValue, valueFormatter = defaultFormat }) {
  const colored = data.map((d, i) => ({ ...d, color: d.color || DK_PALETTE[i % DK_PALETTE.length] }));
  const ring = colored.filter((d) => d.value > 0);
  const total = colored.reduce((s, d) => s + (d.value || 0), 0);

  const size = 200;
  const cx = size / 2;
  const cy = size / 2;
  const rOuter = 95;
  const rInner = 66;
  const gap = ring.length > 1 ? 2 : 0; // degrees of breathing room per slice

  let angle = 0;
  const slices = ring.map((d) => {
    const sweep = total > 0 ? (d.value / total) * 360 : 0;
    const start = angle + gap / 2;
    const end = Math.max(start + 0.5, angle + sweep - gap / 2);
    angle += sweep;
    if (sweep >= 360 - gap) {
      // Single-slice ring: draw two half annuli to avoid degenerate arcs.
      return { ...d, full: true };
    }
    return { ...d, start, end };
  });

  return (
    <div>
      <div className="dk-donut-wrap" style={{ height }}>
        <svg viewBox={`0 0 ${size} ${size}`} width="100%" height="100%" role="img">
          {slices.map((d) =>
            d.full ? (
              <g key={d.name} fill={d.color}>
                <path d={arcPath(cx, cy, rOuter, rInner, 0, 180)}>
                  <title>{`${d.name}: ${valueFormatter(d.value)}`}</title>
                </path>
                <path d={arcPath(cx, cy, rOuter, rInner, 180, 360)}>
                  <title>{`${d.name}: ${valueFormatter(d.value)}`}</title>
                </path>
              </g>
            ) : (
              <path key={d.name} d={arcPath(cx, cy, rOuter, rInner, d.start, d.end)} fill={d.color} className="dk-donut-slice">
                <title>{`${d.name}: ${valueFormatter(d.value)}`}</title>
              </path>
            )
          )}
          {ring.length === 0 && (
            <circle cx={cx} cy={cy} r={(rOuter + rInner) / 2} fill="none" stroke="var(--color-border)" strokeWidth={rOuter - rInner} />
          )}
        </svg>
        <div className="dk-donut-center">
          <b>{centerValue ?? valueFormatter(total)}</b>
          {centerLabel && <span>{centerLabel}</span>}
        </div>
      </div>
      <div className="dk-legend">
        {colored.map((d) => (
          <div className="dk-legend-row" key={d.name}>
            <i className="dk-legend-dot" style={{ background: d.color }} />
            <span className="dk-legend-name">{d.name}</span>
            <span className="dk-legend-value">{valueFormatter(d.value)}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

function niceMax(v) {
  if (v <= 0) return 1;
  const mag = 10 ** Math.floor(Math.log10(v));
  const norm = v / mag;
  const step = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10;
  return step * mag;
}

/** Vertical bars. data: [{ name, value, color? }]. */
export function BarsChart({ data = [], height = 240, color = "#2e3591", valueFormatter = defaultFormat }) {
  const colored = data.map((d, i) => ({ ...d, color: d.color || color || DK_PALETTE[i % DK_PALETTE.length] }));
  const W = 480;
  const H = 240;
  const padL = 8;
  const padR = 8;
  const padT = 18;
  const padB = 26;
  const plotW = W - padL - padR;
  const plotH = H - padT - padB;
  const max = niceMax(Math.max(...colored.map((d) => d.value || 0), 0));
  const n = Math.max(colored.length, 1);
  const slot = plotW / n;
  const barW = Math.min(slot * 0.55, 64);

  return (
    <svg viewBox={`0 0 ${W} ${H}`} width="100%" style={{ height, display: "block" }} role="img">
      {[0.25, 0.5, 0.75, 1].map((f) => (
        <line
          key={f}
          x1={padL}
          x2={W - padR}
          y1={padT + plotH * (1 - f)}
          y2={padT + plotH * (1 - f)}
          stroke="rgba(26,31,94,0.08)"
          strokeDasharray="3 3"
        />
      ))}
      <line x1={padL} x2={W - padR} y1={padT + plotH} y2={padT + plotH} stroke="rgba(26,31,94,0.15)" />
      {colored.map((d, i) => {
        const h = max > 0 ? ((d.value || 0) / max) * plotH : 0;
        const x = padL + slot * i + (slot - barW) / 2;
        const y = padT + plotH - h;
        return (
          <g key={d.name}>
            <rect x={x} y={y} width={barW} height={Math.max(h, d.value > 0 ? 2 : 0)} rx={5} fill={d.color} className="dk-bar">
              <title>{`${d.name}: ${valueFormatter(d.value)}`}</title>
            </rect>
            <text x={x + barW / 2} y={y - 5} textAnchor="middle" className="dk-svg-value">
              {valueFormatter(d.value)}
            </text>
            <text x={padL + slot * i + slot / 2} y={H - 8} textAnchor="middle" className="dk-svg-label">
              {d.name}
            </text>
          </g>
        );
      })}
    </svg>
  );
}

/** Gold area trend. data: [{ name, value }]. */
export function TrendChart({ data = [], height = 240, color = "#c9a84c", valueFormatter = defaultFormat }) {
  const W = 640;
  const H = 250;
  const padL = 64;
  const padR = 14;
  const padT = 14;
  const padB = 26;
  const plotW = W - padL - padR;
  const plotH = H - padT - padB;
  const max = niceMax(Math.max(...data.map((d) => d.value || 0), 0));
  const n = data.length;
  const x = (i) => (n > 1 ? padL + (plotW * i) / (n - 1) : padL + plotW / 2);
  const y = (v) => padT + plotH * (1 - (max > 0 ? (v || 0) / max : 0));

  const line = data.map((d, i) => `${i === 0 ? "M" : "L"} ${x(i)} ${y(d.value)}`).join(" ");
  const area = n > 0 ? `${line} L ${x(n - 1)} ${padT + plotH} L ${x(0)} ${padT + plotH} Z` : "";
  const labelEvery = Math.max(1, Math.ceil(n / 8));
  const gradId = `dk-grad-${color.replace("#", "")}`;

  return (
    <svg viewBox={`0 0 ${W} ${H}`} width="100%" style={{ height, display: "block" }} role="img">
      <defs>
        <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity={0.32} />
          <stop offset="100%" stopColor={color} stopOpacity={0.02} />
        </linearGradient>
      </defs>
      {[0, 0.25, 0.5, 0.75, 1].map((f) => (
        <g key={f}>
          <line
            x1={padL}
            x2={W - padR}
            y1={padT + plotH * (1 - f)}
            y2={padT + plotH * (1 - f)}
            stroke="rgba(26,31,94,0.08)"
            strokeDasharray={f === 0 ? undefined : "3 3"}
          />
          <text x={padL - 8} y={padT + plotH * (1 - f) + 3.5} textAnchor="end" className="dk-svg-label">
            {valueFormatter(max * f)}
          </text>
        </g>
      ))}
      {area && <path d={area} fill={`url(#${gradId})`} />}
      {line && <path d={line} fill="none" stroke={color} strokeWidth={2.5} strokeLinejoin="round" strokeLinecap="round" />}
      {data.map((d, i) => (
        <g key={`${d.name}-${i}`}>
          <circle cx={x(i)} cy={y(d.value)} r={7} fill="transparent" className="dk-trend-dot-hit">
            <title>{`${d.name}: ${valueFormatter(d.value)}`}</title>
          </circle>
          <circle cx={x(i)} cy={y(d.value)} r={3} fill="#fff" stroke={color} strokeWidth={2} pointerEvents="none" />
          {i % labelEvery === 0 && (
            <text x={x(i)} y={H - 8} textAnchor="middle" className="dk-svg-label">
              {d.name}
            </text>
          )}
        </g>
      ))}
    </svg>
  );
}
