import { useEffect, useRef } from "react";
import { AlertTriangle } from "lucide-react";

export default function ConfirmDialog({
  open,
  onClose,
  onConfirm,
  title = "Confirm",
  message = "Are you sure?",
  detail,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  variant = "default",
  loading = false,
  children,
}) {
  const confirmRef = useRef(null);

  useEffect(() => {
    if (open) {
      const timer = setTimeout(() => confirmRef.current?.focus(), 50);
      return () => clearTimeout(timer);
    }
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const handleKeyDown = (e) => {
      if (e.key === "Escape") onClose();
      if (e.key === "Tab") {
        const focusable = confirmRef.current?.closest('[role="dialog"]')?.querySelectorAll(
          'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
        );
        if (!focusable || focusable.length === 0) return;
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [open, onClose]);

  if (!open) return null;

  const variantStyles = {
    default: { iconColor: "#2563eb", confirmBg: "#2563eb" },
    danger: { iconColor: "#dc2626", confirmBg: "#dc2626" },
    warning: { iconColor: "#d97706", confirmBg: "#d97706" },
  };

  const v = variantStyles[variant] || variantStyles.default;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="confirm-dialog-title"
      aria-describedby="confirm-dialog-desc"
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
          width: 420,
          maxWidth: "90vw",
          boxShadow: "0 10px 40px rgba(0,0,0,0.15)",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div style={{ padding: "20px 22px", textAlign: "center" }}>
          <AlertTriangle size={36} style={{ color: v.iconColor, marginBottom: 12 }} />
          <h3
            id="confirm-dialog-title"
            style={{ margin: "0 0 8px", fontSize: 16, color: "#1a1f5e" }}
          >
            {title}
          </h3>
          <p
            id="confirm-dialog-desc"
            style={{ margin: 0, fontSize: 14, color: "#374151", lineHeight: 1.5 }}
          >
            {message}
          </p>
          {detail && (
            <p style={{ fontSize: 12, color: "#6b7280", marginTop: 8, lineHeight: 1.4 }}>
              {detail}
            </p>
          )}
          {children && <div style={{ marginTop: 12 }}>{children}</div>}
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
            {cancelLabel}
          </button>
          <button
            ref={confirmRef}
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
              background: v.confirmBg,
              opacity: loading ? 0.6 : 1,
            }}
          >
            {loading ? `${confirmLabel}...` : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
