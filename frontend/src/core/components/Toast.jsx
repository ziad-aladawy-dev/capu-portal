import { useState, useCallback, createContext, useContext } from "react";
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
  success: { bg: "#f0fdf4", border: "#bbf7d0", icon: "#166534", text: "#166534" },
  error: { bg: "#fef2f2", border: "#fecaca", icon: "#dc2626", text: "#dc2626" },
  warning: { bg: "#fffbeb", border: "#fde68a", icon: "#b45309", text: "#b45309" },
  info: { bg: "#eff6ff", border: "#bfdbfe", icon: "#2563eb", text: "#2563eb" },
};

function ToastContainer({ toasts, removeToast }) {
  if (toasts.length === 0) return null;

  return (
    <div style={{
      position: "fixed",
      top: 16,
      right: 16,
      zIndex: 10000,
      display: "flex",
      flexDirection: "column",
      gap: 8,
      maxWidth: 380,
    }}>
      {toasts.map((toast) => {
        const colors = TOAST_COLORS[toast.type] || TOAST_COLORS.info;
        const Icon = TOAST_ICONS[toast.type] || Info;

        return (
          <div
            key={toast.id}
            style={{
              display: "flex",
              alignItems: "flex-start",
              gap: 10,
              padding: "12px 16px",
              borderRadius: 10,
              background: colors.bg,
              border: `1px solid ${colors.border}`,
              boxShadow: "0 4px 16px rgba(0,0,0,0.1)",
              animation: "toastSlideIn 0.3s ease",
              fontFamily: '"Outfit", sans-serif',
            }}
          >
            <Icon size={18} style={{ color: colors.icon, flexShrink: 0, marginTop: 1 }} />
            <div style={{ flex: 1, color: colors.text, fontSize: 13, lineHeight: 1.5 }}>
              {toast.message}
            </div>
            <button
              onClick={() => removeToast(toast.id)}
              style={{
                background: "none",
                border: "none",
                cursor: "pointer",
                padding: 2,
                color: colors.text,
                opacity: 0.6,
                flexShrink: 0,
              }}
            >
              <X size={14} />
            </button>
          </div>
        );
      })}
    </div>
  );
}

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);

  const addToast = useCallback((message, type = "info", duration = 4000) => {
    const id = ++toastId;
    setToasts((prev) => [...prev, { id, message, type }]);
    if (duration > 0) {
      setTimeout(() => {
        setToasts((prev) => prev.filter((t) => t.id !== id));
      }, duration);
    }
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
