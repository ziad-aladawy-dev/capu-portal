import { useState, useRef, useCallback, useMemo } from "react";

/**
 * VirtualList — dependency-free fixed-height row windowing.
 * Renders only the visible slice (+ overscan) of potentially very large lists
 * (e.g. thousands of offerings/slots) so the DOM stays small and scrolling at 60fps.
 *
 * Props:
 *  - items: T[]
 *  - rowHeight: number (px, fixed)
 *  - height: number (px, viewport height of the scroller)
 *  - renderRow: (item, index) => ReactNode
 *  - overscan?: number (rows rendered above/below the viewport, default 6)
 *  - rowKey?: (item, index) => string|number
 *  - emptyState?: ReactNode
 */
export default function VirtualList({
  items = [],
  rowHeight = 44,
  height = 420,
  renderRow,
  overscan = 6,
  rowKey,
  emptyState = null,
}) {
  const [scrollTop, setScrollTop] = useState(0);
  const ref = useRef(null);

  const onScroll = useCallback((e) => setScrollTop(e.currentTarget.scrollTop), []);

  const { start, end, padTop, totalHeight } = useMemo(() => {
    const total = items.length * rowHeight;
    const first = Math.max(0, Math.floor(scrollTop / rowHeight) - overscan);
    const visible = Math.ceil(height / rowHeight) + overscan * 2;
    const last = Math.min(items.length, first + visible);
    return { start: first, end: last, padTop: first * rowHeight, totalHeight: total };
  }, [items.length, rowHeight, height, scrollTop, overscan]);

  if (items.length === 0) return emptyState;

  const slice = items.slice(start, end);

  return (
    <div
      ref={ref}
      onScroll={onScroll}
      style={{ height, overflowY: "auto", position: "relative" }}
      role="list"
    >
      <div style={{ height: totalHeight, position: "relative" }}>
        <div style={{ transform: `translateY(${padTop}px)` }}>
          {slice.map((item, i) => {
            const index = start + i;
            return (
              <div
                key={rowKey ? rowKey(item, index) : index}
                role="listitem"
                style={{ height: rowHeight }}
              >
                {renderRow(item, index)}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
