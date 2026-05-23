function Skeleton({ width = "100%", height = 16, borderRadius = 6, style = {} }) {
  return (
    <div
      style={{
        width,
        height,
        borderRadius,
        background: "linear-gradient(90deg, #e5e7eb 25%, #f3f4f6 50%, #e5e7eb 75%)",
        backgroundSize: "200% 100%",
        animation: "skeletonPulse 1.5s ease-in-out infinite",
        ...style,
      }}
    />
  );
}

export function SkeletonRow({ cols = 4, height = 14 }) {
  return (
    <div style={{ display: "flex", gap: 16, padding: "12px 0", borderBottom: "1px solid #f0f1f8" }}>
      {Array.from({ length: cols }).map((_, i) => (
        <Skeleton key={i} height={height} style={{ flex: i === 0 ? 2 : 1 }} />
      ))}
    </div>
  );
}

export function SkeletonTable({ rows = 5, cols = 4 }) {
  return (
    <div style={{ background: "white", borderRadius: 12, border: "1px solid #e5e7eb", overflow: "hidden" }}>
      <div style={{ display: "flex", gap: 16, padding: "12px 14px", background: "#1a1f5e" }}>
        {Array.from({ length: cols }).map((_, i) => (
          <Skeleton key={i} height={12} style={{ flex: i === 0 ? 2 : 1, opacity: 0.3 }} />
        ))}
      </div>
      {Array.from({ length: rows }).map((_, r) => (
        <SkeletonRow key={r} cols={cols} />
      ))}
    </div>
  );
}

export function SkeletonCard({ height = 120 }) {
  return (
    <div
      style={{
        background: "white",
        borderRadius: 12,
        border: "1px solid #e5e7eb",
        padding: 20,
      }}
    >
      <Skeleton height={18} style={{ marginBottom: 12, width: "60%" }} />
      <Skeleton height={14} style={{ marginBottom: 8 }} />
      <Skeleton height={14} style={{ width: "80%", marginBottom: 16 }} />
      <div style={{ display: "flex", gap: 8 }}>
        <Skeleton height={32} width={80} borderRadius={8} />
        <Skeleton height={32} width={100} borderRadius={8} />
      </div>
    </div>
  );
}

export function SkeletonStats({ count = 4 }) {
  return (
    <div style={{ display: "grid", gridTemplateColumns: `repeat(${count}, 1fr)`, gap: 16 }}>
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} style={{ background: "white", borderRadius: 12, border: "1px solid #e5e7eb", padding: 20, display: "flex", alignItems: "center", gap: 16 }}>
          <Skeleton width={48} height={48} borderRadius={12} />
          <div style={{ flex: 1 }}>
            <Skeleton height={24} style={{ marginBottom: 6, width: "60%" }} />
            <Skeleton height={14} width="40%" />
          </div>
        </div>
      ))}
    </div>
  );
}

// Inject keyframes once
if (typeof document !== "undefined") {
  const styleId = "skeleton-keyframes";
  if (!document.getElementById(styleId)) {
    const style = document.createElement("style");
    style.id = styleId;
    style.textContent = `
      @keyframes skeletonPulse {
        0% { background-position: 200% 0; }
        100% { background-position: -200% 0; }
      }
    `;
    document.head.appendChild(style);
  }
}

export default Skeleton;
