import { useTranslation } from "react-i18next";
import { getNodeTypeConfig } from "../../modules/university/utils/nodeTypeRegistry";

function NodeTypeBadge({ type, showIcon = true, showLabel = true, size = "sm" }) {
  const { t } = useTranslation();
  const config = getNodeTypeConfig(type);
  if (!config) return null;

  const Icon = config.icon;
  const sizeStyles = size === "lg" ? { fontSize: 11, padding: "3px 8px", gap: 5 }
    : size === "xs" ? { fontSize: 8, padding: "1px 4px", gap: 3 }
    : { fontSize: 9, padding: "2px 6px", gap: 4 };

  return (
    <span
      className="node-type-badge"
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: sizeStyles.gap,
        padding: sizeStyles.padding,
        borderRadius: 999,
        fontSize: sizeStyles.fontSize,
        fontWeight: 700,
        letterSpacing: "0.3px",
        textTransform: "uppercase",
        color: config.color,
        background: config.backgroundColor,
        border: `1px solid ${config.borderColor}`,
        whiteSpace: "nowrap",
        lineHeight: 1.2,
      }}
    >
      {showIcon && <Icon size={size === "xs" ? 8 : size === "lg" ? 12 : 10} style={{ flexShrink: 0 }} />}
      {showLabel && (t(config.labelKey) || config.type)}
    </span>
  );
}

export default NodeTypeBadge;
