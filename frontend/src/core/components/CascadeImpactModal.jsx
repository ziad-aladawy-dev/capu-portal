import { useEffect, useRef, useCallback } from "react";
import { AlertTriangle, ChevronRight } from "lucide-react";

export default function CascadeImpactModal({
  open,
  onClose,
  onConfirm,
  title = "Close Record",
  entityName,
  impacts = [],
  loading = false,
}) {
  const modalRef = useRef(null);

  const trapFocus = useCallback((e) => {
    if (e.key !== "Tab") return;
    const modal = modalRef.current;
    if (!modal) return;
    const focusable = modal.querySelectorAll(
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
    );
    if (focusable.length === 0) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (e.shiftKey && document.activeElement === first) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && document.activeElement === last) {
      e.preventDefault();
      first.focus();
    }
  }, []);

  useEffect(() => {
    if (!open) return;
    const handleKeyDown = (e) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", handleKeyDown);
    document.addEventListener("keydown", trapFocus);
    const timer = setTimeout(() => {
      const confirmBtn = modalRef.current?.querySelector('button:not([class*="btn-cancel"])');
      if (confirmBtn) confirmBtn.focus();
    }, 50);
    return () => {
      document.removeEventListener("keydown", handleKeyDown);
      document.removeEventListener("keydown", trapFocus);
      clearTimeout(timer);
    };
  }, [open, onClose, trapFocus]);

  if (!open) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="cascade-title"
      aria-describedby="cascade-desc"
      ref={modalRef}
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(15,17,47,0.55)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: 1000,
      }}
      onClick={onClose}
    >
      <div
        style={{
          background: "white",
          borderRadius: 14,
          width: 480,
          maxWidth: "90vw",
          boxShadow: "0 10px 40px rgba(0,0,0,0.15)",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div style={{ padding: "20px 22px" }}>
          <div style={{ textAlign: "center", marginBottom: 16 }}>
            <AlertTriangle size={36} style={{ color: "#d97706", marginBottom: 8 }} />
            <h3 id="cascade-title" style={{ margin: "0 0 4px", fontSize: 16, color: "#1a1f5e" }}>
              {title}
            </h3>
            <p id="cascade-desc" style={{ margin: 0, fontSize: 14, color: "#374151" }}>
              This action will affect the following items:
            </p>
          </div>

          {entityName && (
            <div
              style={{
                padding: "8px 12px",
                background: "#f8f9fc",
                borderRadius: 8,
                border: "1px solid #e5e7eb",
                marginBottom: 12,
                fontWeight: 600,
                fontSize: 13,
                color: "#1a1f5e",
              }}
            >
              {entityName}
            </div>
          )}

          {impacts.length > 0 && (
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              {impacts.map((item, idx) => (
                <div
                  key={idx}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 8,
                    padding: "8px 12px",
                    background: "#fefce8",
                    borderRadius: 8,
                    border: "1px solid #fde68a",
                    fontSize: 13,
                    color: "#92400e",
                  }}
                >
                  <ChevronRight size={14} style={{ flexShrink: 0 }} />
                  <span>
                    <strong>{item.count}</strong> {item.label}
                    {item.detail && (
                      <span style={{ color: "#6b7280", marginLeft: 4 }}>({item.detail})</span>
                    )}
                  </span>
                </div>
              ))}
            </div>
          )}

          <p style={{ fontSize: 12, color: "#6b7280", marginTop: 12, textAlign: "center" }}>
            This action cannot be undone. Affected items will be marked as closed.
          </p>
        </div>

        <div
          style={{
            display: "flex",
            justifyContent: "flex-end",
            gap: 8,
            padding: "12px 22px",
            borderTop: "1px solid #f3f4f6",
          }}
        >
          <button
            className="btn-cancel"
            onClick={onClose}
            disabled={loading}
            style={{ padding: "8px 16px", fontSize: 13 }}
          >
            Cancel
          </button>
          <button
            onClick={onConfirm}
            disabled={loading}
            style={{
              padding: "8px 20px",
              border: "none",
              borderRadius: 8,
              fontSize: 13,
              fontWeight: 600,
              cursor: "pointer",
              color: "white",
              background: "#d97706",
              opacity: loading ? 0.6 : 1,
            }}
          >
            {loading ? "Closing..." : `Yes, Close ${entityName || "Record"}`}
          </button>
        </div>
      </div>
    </div>
  );
}
