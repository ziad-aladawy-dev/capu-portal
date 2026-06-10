import { useState, useRef, useCallback } from "react";

/**
 * EntityHoverCard — wraps an inline reference (a CourseID, StudentID, etc.)
 * so hovering reveals a floating quick-stats popover, and clicking can deep-link
 * into a quick-view. Cross-referencing primitive used across the academic workspaces.
 *
 * Props:
 *  - trigger: ReactNode rendered inline (the clickable/hoverable label)
 *  - rows: Array<{ label, value }> — quick stats shown in the popover
 *  - title?: string — popover heading
 *  - onClick?: () => void — invoked when the trigger is clicked
 *  - loading?: boolean
 */
export default function EntityHoverCard({ trigger, rows = [], title, onClick, loading = false }) {
  const [open, setOpen] = useState(false);
  const [coords, setCoords] = useState({ top: 0, left: 0 });
  const anchorRef = useRef(null);
  const timer = useRef(null);

  const show = useCallback(() => {
    clearTimeout(timer.current);
    timer.current = setTimeout(() => {
      const el = anchorRef.current;
      if (!el) return;
      const r = el.getBoundingClientRect();
      setCoords({ top: r.bottom + 6, left: Math.max(8, r.left) });
      setOpen(true);
    }, 120);
  }, []);

  const hide = useCallback(() => {
    clearTimeout(timer.current);
    setOpen(false);
  }, []);

  return (
    <span
      ref={anchorRef}
      onMouseEnter={show}
      onMouseLeave={hide}
      onFocus={show}
      onBlur={hide}
      style={{ position: "relative", display: "inline-flex" }}
    >
      <span
        role={onClick ? "button" : undefined}
        tabIndex={onClick ? 0 : undefined}
        onClick={onClick}
        onKeyDown={(e) => {
          if (onClick && (e.key === "Enter" || e.key === " ")) {
            e.preventDefault();
            onClick();
          }
        }}
        style={{
          cursor: onClick ? "pointer" : "help",
          borderBottom: "1px dashed #c7cbe0",
          color: onClick ? "#1a1f5e" : "inherit",
          fontWeight: onClick ? 600 : "inherit",
        }}
      >
        {trigger}
      </span>

      {open && (
        <div
          role="tooltip"
          style={{
            position: "fixed",
            top: coords.top,
            left: coords.left,
            zIndex: 1100,
            minWidth: 220,
            maxWidth: 320,
            background: "white",
            border: "1px solid #e5e7eb",
            borderRadius: 10,
            boxShadow: "0 12px 32px rgba(15,17,47,0.18)",
            padding: "12px 14px",
            fontFamily: '"Outfit", sans-serif',
            animation: "hovercardIn 0.12s ease-out",
          }}
        >
          {title && (
            <div
              style={{
                fontSize: 13,
                fontWeight: 700,
                color: "#1a1f5e",
                marginBottom: 8,
                paddingBottom: 6,
                borderBottom: "1px solid #f3f4f6",
              }}
            >
              {title}
            </div>
          )}
          {loading ? (
            <div style={{ fontSize: 12, color: "#9ca3af" }}>Loading…</div>
          ) : rows.length === 0 ? (
            <div style={{ fontSize: 12, color: "#9ca3af" }}>No details available.</div>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
              {rows.map((r, i) => (
                <div key={i} style={{ display: "flex", justifyContent: "space-between", gap: 12, fontSize: 12 }}>
                  <span style={{ color: "#6b7280" }}>{r.label}</span>
                  <span style={{ color: "#374151", fontWeight: 600, textAlign: "right" }}>{r.value}</span>
                </div>
              ))}
            </div>
          )}
          {onClick && (
            <div style={{ marginTop: 10, fontSize: 11, color: "#1a1f5e", fontWeight: 600 }}>
              Click to open →
            </div>
          )}
          <style>{`@keyframes hovercardIn { from { opacity: 0; transform: translateY(-3px); } to { opacity: 1; transform: translateY(0); } }`}</style>
        </div>
      )}
    </span>
  );
}
