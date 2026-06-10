import { useMemo } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { SkeletonTable } from "./Skeleton";
import EmptyState from "./EmptyState";

export default function DataTable({
  columns = [],
  data = [],
  loading = false,
  error = null,
  emptyIcon,
  emptyTitle = "No data found",
  emptyMessage = "No records match your criteria.",
  emptyAction,
  emptyActionLabel,
  pagination,
  onPageChange,
  selectedIds = new Set(),
  onSelectAll,
  onSelectOne,
  rowKey = "id",
  onRowClick,
  getRowClass,
  skeletonRows = 6,
  compact = false,
  onCompactToggle,
  tableLabel,
}) {
  const allSelected = data.length > 0 && data.every((r) => selectedIds.has(r[rowKey]));
  const someSelected = data.some((r) => selectedIds.has(r[rowKey]));

  const hasSelection = onSelectAll && onSelectOne;

  const paginationButtons = useMemo(() => {
    if (!pagination || pagination.totalPages <= 1) return null;
    const { pageNumber, totalPages } = pagination;
    const buttons = [];
    const maxVisible = 5;
    let start, end;
    if (totalPages <= maxVisible) {
      start = 1;
      end = totalPages;
    } else {
      const mid = Math.floor(maxVisible / 2);
      if (pageNumber <= mid + 1) {
        start = 1;
        end = maxVisible;
      } else if (pageNumber >= totalPages - mid) {
        start = totalPages - maxVisible + 1;
        end = totalPages;
      } else {
        start = pageNumber - mid;
        end = pageNumber + mid;
      }
    }
    for (let i = start; i <= end; i++) {
      buttons.push(i);
    }
    return { pageNumber, totalPages, buttons };
  }, [pagination]);

  if (loading) {
    return <SkeletonTable rows={skeletonRows} cols={columns.length || 4} />;
  }

  if (error) {
    return (
      <div
        style={{
          padding: 16,
          background: "#fef2f2",
          border: "1px solid #fecaca",
          borderRadius: 8,
          color: "#b91c1c",
          fontSize: 13,
          display: "flex",
          alignItems: "center",
          gap: 8,
        }}
      >
        <span role="alert">{error}</span>
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <EmptyState
        icon={emptyIcon}
        title={emptyTitle}
        message={emptyMessage}
        actionLabel={emptyActionLabel}
        onAction={emptyAction}
      />
    );
  }

  const cellPadding = compact ? "4px 8px" : "8px 10px";
  const fontSize = compact ? 12 : 13;

  return (
    <div>
      <div
        style={{
          display: "flex",
          justifyContent: "flex-end",
          marginBottom: 6,
          gap: 8,
          alignItems: "center",
        }}
      >
        <span
          role="status"
          aria-live="polite"
          style={{ fontSize: 12, color: "#6b7280" }}
        >
          {data.length} record(s)
        </span>
        {onCompactToggle && (
          <label
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: 4,
              fontSize: 11,
              color: "#6b7280",
              cursor: "pointer",
            }}
          >
            <input
              type="checkbox"
              checked={compact}
              onChange={onCompactToggle}
              style={{ accentColor: "#1a1f5e" }}
            />
            Compact
          </label>
        )}
      </div>
      <div style={{ overflowX: "auto", borderRadius: 8, border: "1px solid #e5e7eb" }}>
        <table aria-label={tableLabel || "Data table"} style={{ width: "100%", borderCollapse: "collapse", fontSize }}>
          <thead>
            <tr style={{ background: "#f8f9fc", borderBottom: "1px solid #e5e7eb" }}>
              {hasSelection && (
                <th style={{ width: 36, padding: cellPadding, textAlign: "center" }}>
                  <input
                    type="checkbox"
                    checked={allSelected}
                    ref={(el) => {
                      if (el) el.indeterminate = someSelected && !allSelected;
                    }}
                    onChange={onSelectAll}
                    aria-label="Select all rows"
                  />
                </th>
              )}
              {columns.map((col) => (
                <th
                  key={col.key}
                  style={{
                    padding: cellPadding,
                    textAlign: col.align || "left",
                    fontWeight: 600,
                    fontSize: compact ? 10 : 11,
                    color: "#6b7280",
                    textTransform: "uppercase",
                    letterSpacing: "0.05em",
                    whiteSpace: "nowrap",
                    width: col.width,
                    ...col.headerStyle,
                  }}
                >
                  {col.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.map((row, idx) => (
              <tr
                key={row[rowKey]}
                className={selectedIds.has(row[rowKey]) ? "selected-row" : ""}
                onClick={() => onRowClick?.(row)}
                style={{
                  borderBottom: "1px solid #f3f4f6",
                  cursor: onRowClick ? "pointer" : "default",
                  transition: "background 0.15s",
                  ...(getRowClass?.(row) === "warn"
                    ? { background: "#fffbeb" }
                    : getRowClass?.(row) === "danger"
                    ? { background: "#fef2f2" }
                    : {}),
                }}
                onMouseEnter={(e) => {
                  if (!selectedIds.has(row[rowKey])) e.currentTarget.style.background = "#f9fafb";
                }}
                onMouseLeave={(e) => {
                  if (!selectedIds.has(row[rowKey])) e.currentTarget.style.background = "";
                }}
              >
                {hasSelection && (
                  <td
                    style={{ padding: cellPadding, textAlign: "center" }}
                    onClick={(e) => e.stopPropagation()}
                  >
                    <input
                      type="checkbox"
                      checked={selectedIds.has(row[rowKey])}
                      onChange={() => onSelectOne(row[rowKey])}
                      aria-label={`Select row ${idx + 1}`}
                    />
                  </td>
                )}
                {columns.map((col) => (
                  <td
                    key={col.key}
                    style={{
                      padding: cellPadding,
                      textAlign: col.align || "left",
                      fontSize,
                      color: "#374151",
                      whiteSpace: col.nowrap !== false ? "nowrap" : undefined,
                      ...col.cellStyle,
                    }}
                  >
                    {col.render ? col.render(row[col.key], row) : row[col.key] ?? "—"}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {paginationButtons && (
        <div
          style={{
            display: "flex",
            justifyContent: "center",
            alignItems: "center",
            gap: 6,
            marginTop: compact ? 8 : 16,
          }}
        >
          <button
            className="btn-cancel"
            disabled={paginationButtons.pageNumber <= 1}
            onClick={() => onPageChange(paginationButtons.pageNumber - 1)}
            style={{ padding: "4px 10px", fontSize: compact ? 11 : 12 }}
            aria-label="Previous page"
          >
            <ChevronLeft size={compact ? 12 : 14} /> Previous
          </button>
          {paginationButtons.buttons.map((pn) => (
            <button
              key={pn}
              className={pn === paginationButtons.pageNumber ? "btn-primary" : "btn-cancel"}
              onClick={() => onPageChange(pn)}
              style={{
                minWidth: compact ? 28 : 32,
                justifyContent: "center",
                padding: "4px 6px",
                fontSize: compact ? 11 : 12,
                border: pn === paginationButtons.pageNumber ? "none" : undefined,
              }}
              aria-current={pn === paginationButtons.pageNumber ? "page" : undefined}
              aria-label={`Page ${pn}`}
            >
              {pn}
            </button>
          ))}
          <button
            className="btn-cancel"
            disabled={paginationButtons.pageNumber >= paginationButtons.totalPages}
            onClick={() => onPageChange(paginationButtons.pageNumber + 1)}
            style={{ padding: "4px 10px", fontSize: compact ? 11 : 12 }}
            aria-label="Next page"
          >
            Next <ChevronRight size={compact ? 12 : 14} />
          </button>
        </div>
      )}
    </div>
  );
}
