import { useEffect, useRef, useCallback } from "react";
import { X } from "lucide-react";

export default function Drawer({
  open,
  onClose,
  title,
  children,
  footer,
  width = 480,
  loading = false,
}) {
  const drawerRef = useRef(null);
  // Keep the latest onClose in a ref so the key-handler effect doesn't have to
  // depend on it. Parents commonly pass an inline (non-memoized) onClose, which
  // changes identity on every render; without this, every parent re-render (e.g.
  // typing into a field) would re-run the effect below and call
  // drawerRef.focus(), yanking focus out of the active input on each keystroke.
  const onCloseRef = useRef(onClose);
  const titleId = "drawer-title";

  useEffect(() => {
    onCloseRef.current = onClose;
  });

  const trapFocus = useCallback((e) => {
    const dialog = drawerRef.current;
    if (!dialog) return;
    const focusable = dialog.querySelectorAll(
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

  // Focus the drawer + lock body scroll ONLY when `open` transitions. Decoupled
  // from onClose so re-renders while open never steal focus.
  useEffect(() => {
    if (!open) return;
    drawerRef.current?.focus();
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = "";
    };
  }, [open]);

  // Key handling (Escape + focus trap). trapFocus is stable; onClose is read
  // through a ref, so this subscribes once per open rather than every render.
  useEffect(() => {
    if (!open) return;
    const handleKeyDown = (e) => {
      if (e.key === "Escape") onCloseRef.current?.();
      else if (e.key === "Tab") trapFocus(e);
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [open, trapFocus]);

  if (!open) return null;

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(15,17,47,0.45)",
        zIndex: 900,
        display: "flex",
        justifyContent: "flex-end",
      }}
      onClick={onClose}
    >
      <div
        ref={drawerRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        style={{
          width,
          maxWidth: "100vw",
          height: "100vh",
          background: "white",
          boxShadow: "-8px 0 32px rgba(0,0,0,0.12)",
          display: "flex",
          flexDirection: "column",
          animation: "drawerSlideIn 0.25s ease-out",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "16px 20px",
            borderBottom: "1px solid #f3f4f6",
            flexShrink: 0,
          }}
        >
          <h2 id={titleId} style={{ margin: 0, fontSize: 16, color: "#1a1f5e" }}>{title}</h2>
          <button
            onClick={onClose}
            disabled={loading}
            style={{
              background: "transparent",
              border: "none",
              cursor: "pointer",
              color: "#6b7280",
              padding: 4,
              display: "flex",
              borderRadius: 6,
            }}
            aria-label="Close drawer"
          >
            <X size={18} />
          </button>
        </div>

        <div style={{ flex: 1, overflowY: "auto", padding: "20px" }}>
          {children}
        </div>

        {footer && (
          <div
            style={{
              display: "flex",
              justifyContent: "flex-end",
              gap: 8,
              padding: "12px 20px",
              borderTop: "1px solid #f3f4f6",
              flexShrink: 0,
            }}
          >
            {footer}
          </div>
        )}
      </div>
      <style>{`
        @keyframes drawerSlideIn {
          from { transform: translateX(100%); }
          to { transform: translateX(0); }
        }
      `}</style>
    </div>
  );
}
