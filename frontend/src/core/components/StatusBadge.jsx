import { Pen, Play, Check, X, Circle, Square } from "lucide-react";

const STATUS_STYLES = {
  draft: {
    background: "#fef3c7",
    color: "#92400e",
    border: "1px solid #fcd34d",
    Icon: Pen,
  },
  open: {
    background: "#dcfce7",
    color: "#166534",
    border: "1px solid #86efac",
    Icon: Play,
  },
  closed: {
    background: "#f3f4f6",
    color: "#6b7280",
    border: "1px solid #d1d5db",
    Icon: X,
  },
  cancelled: {
    background: "#fce4ec",
    color: "#c62828",
    border: "1px solid #ef9a9a",
    Icon: X,
  },
  active: {
    background: "#dcfce7",
    color: "#166534",
    border: "1px solid #86efac",
    Icon: Check,
  },
  inactive: {
    background: "#f3f4f6",
    color: "#6b7280",
    border: "1px solid #d1d5db",
    Icon: Circle,
  },
  full: {
    background: "#fce4ec",
    color: "#c62828",
    border: "1px solid #ef9a9a",
    Icon: Square,
  },
};

const STATUS_LABELS = {
  draft: "Draft",
  open: "Open",
  closed: "Closed",
  cancelled: "Cancelled",
  active: "Active",
  inactive: "Inactive",
  full: "Full",
};

export default function StatusBadge({ status, label, style: extraStyle }) {
  const config = STATUS_STYLES[status] || STATUS_STYLES.inactive;
  const displayLabel = label || STATUS_LABELS[status] || status;
  const { Icon, ...rest } = config;

  return (
    <span
      role="status"
      aria-label={`Status: ${displayLabel}`}
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 4,
        padding: "2px 8px",
        borderRadius: 6,
        fontSize: 11,
        fontWeight: 600,
        lineHeight: "18px",
        whiteSpace: "nowrap",
        ...rest,
        ...extraStyle,
      }}
    >
      <Icon size={10} aria-hidden="true" style={{ flexShrink: 0 }} />
      {displayLabel}
    </span>
  );
}
