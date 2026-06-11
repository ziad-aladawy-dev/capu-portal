import styles from "./PortalCard.module.css";

/**
 * Elevated portal surface. `interactive` adds hover lift; `padding="none"`
 * lets content (lists, images) bleed to the edges.
 */
function PortalCard({ interactive = false, padding = "default", className = "", children, ...rest }) {
  const classes = [
    styles.card,
    interactive ? styles.interactive : "",
    padding === "none" ? styles.flush : "",
    className,
  ].filter(Boolean).join(" ");

  return (
    <div className={classes} {...rest}>
      {children}
    </div>
  );
}

export default PortalCard;
