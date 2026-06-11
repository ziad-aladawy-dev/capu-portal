import styles from "./PortalSkeleton.module.css";

/**
 * Shimmering placeholder. `variant`: "line" (default), "circle", "block".
 * Compose lists with <PortalSkeleton.Lines count={3} />.
 */
function PortalSkeleton({ variant = "line", width, height, className = "", style }) {
  const cls = [
    styles.skeleton,
    variant === "circle" ? styles.circle : variant === "block" ? styles.block : styles.line,
    className,
  ].filter(Boolean).join(" ");
  return <span className={cls} style={{ width, height, ...style }} aria-hidden="true" />;
}

function Lines({ count = 3, gap = 10 }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap }}>
      {Array.from({ length: count }).map((_, i) => (
        <PortalSkeleton key={i} width={`${92 - i * 14}%`} />
      ))}
    </div>
  );
}

PortalSkeleton.Lines = Lines;

export default PortalSkeleton;
