import { X } from "lucide-react";
import PermissionGate from "../auth/PermissionGate";

const btnBase = {
  display: "inline-flex",
  alignItems: "center",
  gap: 6,
  padding: "8px 14px",
  border: "none",
  borderRadius: 8,
  fontSize: 13,
  fontWeight: 600,
  cursor: "pointer",
  whiteSpace: "nowrap",
};

const styles = {
  bar: {
    position: "fixed",
    bottom: 24,
    left: "50%",
    transform: "translateX(-50%)",
    zIndex: 999,
    display: "flex",
    alignItems: "center",
    gap: 12,
    background: "#1a1f5e",
    color: "#fff",
    padding: "10px 20px",
    borderRadius: 12,
    boxShadow: "0 8px 32px rgba(0,0,0,0.25)",
  },
  count: {
    fontSize: 14,
    fontWeight: 700,
    marginRight: 4,
  },
  divider: {
    width: 1,
    height: 24,
    background: "rgba(255,255,255,0.15)",
  },
  close: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    width: 28,
    height: 28,
    border: "none",
    background: "rgba(255,255,255,0.1)",
    borderRadius: 6,
    color: "#fff",
    cursor: "pointer",
    marginLeft: 4,
  },
};

function BulkActionBar({ selectedCount, onClear, actions }) {
  if (selectedCount === 0) return null;

  return (
    <div style={styles.bar}>
      <span style={styles.count}>{selectedCount}</span>
      <span style={{ fontSize: 13, opacity: 0.8 }}>selected</span>
      <div style={styles.divider} />
      {actions.map((action, i) =>
        action.requiresPermission ? (
          <PermissionGate
            key={i}
            resource={action.permissionResource}
            minLevel={action.permissionLevel}
          >
            <button
              style={{
                ...btnBase,
                background: action.variant === "danger"
                  ? "#dc2626"
                  : action.variant === "success"
                  ? "#16a34a"
                  : action.variant === "warning"
                  ? "#d97706"
                  : "rgba(255,255,255,0.12)",
                color: "#fff",
              }}
              onClick={() => action.onClick()}
              disabled={action.disabled}
            >
              {action.icon}
              {action.label}
            </button>
          </PermissionGate>
        ) : (
          <button
            key={i}
            style={{
              ...btnBase,
              background: action.variant === "danger"
                ? "#dc2626"
                : action.variant === "success"
                ? "#16a34a"
                : action.variant === "warning"
                ? "#d97706"
                : "rgba(255,255,255,0.12)",
              color: "#fff",
            }}
            onClick={() => action.onClick()}
            disabled={action.disabled}
          >
            {action.icon}
            {action.label}
          </button>
        )
      )}
      <button style={styles.close} onClick={onClear} title="Clear selection">
        <X size={14} />
      </button>
    </div>
  );
}

export default BulkActionBar;
