import {
  Pen,
  Play,
  Check,
  CheckCheck,
  X,
  Circle,
  Square,
  Clock,
  Eye,
  CreditCard,
} from "lucide-react";

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
  pending: {
    background: "#fef3c7",
    color: "#92400e",
    border: "1px solid #fcd34d",
    Icon: Clock,
  },
  review: {
    background: "#dbeafe",
    color: "#1e40af",
    border: "1px solid #93c5fd",
    Icon: Eye,
  },
  approved: {
    background: "#dcfce7",
    color: "#166534",
    border: "1px solid #86efac",
    Icon: Check,
  },
  rejected: {
    background: "#fee2e2",
    color: "#b91c1c",
    border: "1px solid #fca5a5",
    Icon: X,
  },
  completed: {
    background: "#e0e7ff",
    color: "#3730a3",
    border: "1px solid #a5b4fc",
    Icon: CheckCheck,
  },
  payment: {
    background: "#fed7aa",
    color: "#9a3412",
    border: "1px solid #fdba74",
    Icon: CreditCard,
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
  pending: "Pending",
  review: "In Review",
  approved: "Approved",
  rejected: "Rejected",
  completed: "Completed",
  payment: "Payment Due",
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
