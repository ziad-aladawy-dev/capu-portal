import { useState, useCallback, useEffect, useRef, createContext, useContext } from "react";
import { X, AlertCircle, CheckCircle, Info, AlertTriangle } from "lucide-react";

const ToastContext = createContext(null);

let toastId = 0;

const TOAST_ICONS = {
  success: CheckCircle,
  error: AlertCircle,
  warning: AlertTriangle,
  info: Info,
};

const TOAST_COLORS = {
  success: { bg: "#f0fdf4", border: "#86efac", icon: "#16a34a", text: "#15803d", accent: "#22c55e" },
  error:   { bg: "#fef2f2", border: "#fca5a5", icon: "#dc2626", text: "#b91c1c", accent: "#ef4444" },
  warning: { bg: "#fffbeb", border: "#fcd34d", icon: "#d97706", text: "#92400e", accent: "#f59e0b" },
  info:    { bg: "#eff6ff", border: "#93c5fd", icon: "#2563eb", text: "#1e40af", accent: "#3b82f6" },
};

const DURATION_DEFAULT = 4000;

/* ─── Single Toast Item ─── */
function ToastItem({ toast, onRemove }) {
  const [exiting, setExiting] = useState(false);
  const timerRef = useRef(null);
  const colors = TOAST_COLORS[toast.type] || TOAST_COLORS.info;
  const Icon = TOAST_ICONS[toast.type] || Info;

  const dismiss = useCallback(() => {
    setExiting(true);
    setTimeout(() => onRemove(toast.id), 300);
  }, [toast.id, onRemove]);

  useEffect(() => {
    if (toast.duration > 0) {
      timerRef.current = setTimeout(dismiss, toast.duration);
    }
    return () => clearTimeout(timerRef.current);
  }, [toast.duration, dismiss]);

  // Pause timer on hover
  const handleMouseEnter = () => clearTimeout(timerRef.current);
  const handleMouseLeave = () => {
    if (toast.duration > 0) {
      timerRef.current = setTimeout(dismiss, 1500);
    }
  };

  return (
    <div
      role="alert"
      aria-live="assertive"
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
      style={{
        display: "flex",
        alignItems: "flex-start",
        gap: 12,
        padding: "14px 18px",
        borderRadius: 12,
        background: colors.bg,
        borderLeft: `4px solid ${colors.accent}`,
        boxShadow: "0 8px 30px rgba(0,0,0,0.12), 0 2px 8px rgba(0,0,0,0.06)",
        fontFamily: '"Outfit", sans-serif',
        animation: exiting
          ? "toastSlideOut 0.3s ease forwards"
          : "toastSlideIn 0.35s cubic-bezier(0.175, 0.885, 0.32, 1.275)",
        transform: exiting ? undefined : "translateX(0)",
        opacity: exiting ? 0 : 1,
        maxWidth: 400,
        minWidth: 300,
        backdropFilter: "blur(8px)",
        cursor: "pointer",
        position: "relative",
        overflow: "hidden",
      }}
      onClick={dismiss}
    >
      <div style={{
        width: 32, height: 32, borderRadius: 8,
        background: `${colors.accent}15`,
        display: "flex", alignItems: "center", justifyContent: "center",
        flexShrink: 0,
      }}>
        <Icon size={18} style={{ color: colors.icon }} />
      </div>
      <div style={{ flex: 1, minWidth: 0 }}>
        {toast.title && (
          <div style={{
            fontWeight: 700, fontSize: 13, color: colors.text,
            marginBottom: 2, lineHeight: 1.3,
          }}>
            {toast.title}
          </div>
        )}
        <div style={{
          color: colors.text, fontSize: 13, lineHeight: 1.5,
          opacity: toast.title ? 0.85 : 1,
          fontWeight: toast.title ? 400 : 500,
        }}>
          {toast.message}
        </div>
      </div>
      <button
        onClick={(e) => { e.stopPropagation(); dismiss(); }}
        aria-label="Dismiss notification"
        style={{
          background: "none", border: "none", cursor: "pointer",
          padding: 4, color: colors.text, opacity: 0.4,
          flexShrink: 0, borderRadius: 4,
          transition: "opacity 0.15s",
        }}
        onMouseOver={(e) => e.currentTarget.style.opacity = "0.8"}
        onMouseOut={(e) => e.currentTarget.style.opacity = "0.4"}
      >
        <X size={14} />
      </button>

      {/* Progress bar */}
      {toast.duration > 0 && (
        <div style={{
          position: "absolute", bottom: 0, left: 0, right: 0, height: 3,
          background: `${colors.accent}20`,
          overflow: "hidden",
        }}>
          <div style={{
            height: "100%",
            background: colors.accent,
            animation: `toastProgress ${toast.duration}ms linear forwards`,
            transformOrigin: "left",
          }} />
        </div>
      )}
    </div>
  );
}

/* ─── Toast Container ─── */
function ToastContainer({ toasts, removeToast }) {
  if (toasts.length === 0) return null;

  return (
    <>
      <style>{`
        @keyframes toastSlideIn {
          from { transform: translateX(120%); opacity: 0; }
          to   { transform: translateX(0);    opacity: 1; }
        }
        @keyframes toastSlideOut {
          from { transform: translateX(0);    opacity: 1; }
          to   { transform: translateX(120%); opacity: 0; }
        }
        @keyframes toastProgress {
          from { transform: scaleX(1); }
          to   { transform: scaleX(0); }
        }
      `}</style>
      <div style={{
        position: "fixed",
        top: 20,
        right: 20,
        zIndex: 10000,
        display: "flex",
        flexDirection: "column",
        gap: 10,
        pointerEvents: "none",
      }}>
        {toasts.map((toast) => (
          <div key={toast.id} style={{ pointerEvents: "auto" }}>
            <ToastItem toast={toast} onRemove={removeToast} />
          </div>
        ))}
      </div>
    </>
  );
}

/* ─── Provider ─── */
export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);

  const addToast = useCallback((message, type = "info", duration = DURATION_DEFAULT) => {
    const id = ++toastId;
    // Support object form: addToast({ title, message }, type, duration)
    const payload = typeof message === "object"
      ? { id, type, duration, title: message.title, message: message.message }
      : { id, type, duration, title: null, message };
    setToasts((prev) => [...prev, payload]);
    return id;
  }, []);

  const removeToast = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  return (
    <ToastContext.Provider value={{ addToast, removeToast }}>
      {children}
      <ToastContainer toasts={toasts} removeToast={removeToast} />
    </ToastContext.Provider>
  );
}

export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used within ToastProvider");
  return ctx;
}

export default ToastProvider;
