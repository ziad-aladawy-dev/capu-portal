import { useState, useRef, useEffect } from "react";
import { Columns } from "lucide-react";

function ColumnVisibilityDropdown({ columns, visibleColumns, onToggle, onReset }) {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);

  useEffect(() => {
    if (!open) return;
    const handle = (e) => {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    };
    document.addEventListener("mousedown", handle);
    return () => document.removeEventListener("mousedown", handle);
  }, [open]);

  const toggleable = columns.filter(col => !col.always);

  return (
    <div className="colvis-wrapper" ref={ref} style={{ position: "relative" }}>
      <button
        type="button"
        className="users-secondary-btn"
        onClick={() => setOpen(o => !o)}
        title="Columns"
      >
        <Columns size={15} /> Columns
      </button>
      {open && (
        <div
          style={{
            position: "absolute",
            top: "calc(100% + 6px)",
            insetInlineEnd: 0,
            zIndex: 1000,
            background: "#fff",
            border: "1px solid #e5e7eb",
            borderRadius: 12,
            boxShadow: "0 8px 24px rgba(26,31,94,0.12)",
            padding: "8px 0",
            minWidth: 200,
          }}
        >
          <div style={{ padding: "4px 14px 8px", fontSize: 10, textTransform: "uppercase", letterSpacing: "0.5px", color: "#9ca3af", fontWeight: 700 }}>
            Show/Hide Columns
          </div>
          {toggleable.map(col => (
            <label
              key={col.key}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 10,
                padding: "6px 14px",
                cursor: "pointer",
                fontSize: 13,
                color: "#1a1f5e",
                transition: "background 0.15s",
              }}
              onMouseEnter={e => e.currentTarget.style.background = "#f8f9fb"}
              onMouseLeave={e => e.currentTarget.style.background = "transparent"}
            >
              <input
                type="checkbox"
                checked={visibleColumns.has(col.key)}
                onChange={() => onToggle(col.key)}
                style={{ accentColor: "#c9a84c" }}
              />
              {col.labelKey}
            </label>
          ))}
          <div style={{ borderTop: "1px solid #edf0f5", marginTop: 6, paddingTop: 6 }}>
            <button
              type="button"
              onClick={onReset}
              style={{
                width: "100%",
                border: "none",
                background: "none",
                cursor: "pointer",
                padding: "6px 14px",
                fontSize: 12,
                fontWeight: 600,
                color: "#6b7280",
                textAlign: "start",
              }}
              onMouseEnter={e => e.currentTarget.style.color = "#1a1f5e"}
              onMouseLeave={e => e.currentTarget.style.color = "#6b7280"}
            >
              Reset to Default
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

export default ColumnVisibilityDropdown;
