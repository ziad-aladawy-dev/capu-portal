/**
 * CapacityBar — compact registered/capacity progress indicator.
 * Reused across the Course Hub and Scheduling Matrix to visualise
 * enrollment pressure (RegisteredCount vs Capacity) at a glance.
 */
export default function CapacityBar({
  registered = 0,
  capacity = 0,
  width = 120,
  showLabel = true,
  compact = false,
}) {
  const pct = capacity > 0 ? Math.round((registered / capacity) * 100) : 0;
  const level = pct >= 100 ? "full" : pct >= 80 ? "warn" : "ok";

  const colors = {
    ok: { fill: "#22c55e", text: "#166534" },
    warn: { fill: "#f59e0b", text: "#b45309" },
    full: { fill: "#ef4444", text: "#b91c1c" },
  }[level];

  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, width }}>
      <div
        style={{
          flex: 1,
          height: compact ? 5 : 7,
          background: "#f1f5f9",
          borderRadius: 999,
          overflow: "hidden",
        }}
        role="progressbar"
        aria-valuenow={registered}
        aria-valuemin={0}
        aria-valuemax={capacity}
        aria-label={`${registered} of ${capacity} seats filled`}
      >
        <div
          style={{
            width: `${Math.min(pct, 100)}%`,
            height: "100%",
            background: colors.fill,
            borderRadius: 999,
            transition: "width 0.3s ease",
          }}
        />
      </div>
      {showLabel && (
        <span
          style={{
            fontSize: compact ? 11 : 12,
            fontWeight: 600,
            color: colors.text,
            whiteSpace: "nowrap",
            fontVariantNumeric: "tabular-nums",
          }}
        >
          {registered}/{capacity || "∞"}
        </span>
      )}
    </div>
  );
}
