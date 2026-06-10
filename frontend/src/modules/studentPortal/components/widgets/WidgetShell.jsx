import { memo } from "react";
import { Link } from "react-router-dom";
import { AlertCircle, RefreshCw, ArrowRight } from "lucide-react";
import Skeleton from "../../../../core/components/Skeleton";

/**
 * Standard chrome for every dashboard widget: title bar, optional "view all"
 * link, and the four canonical async states (loading skeleton, error+retry,
 * empty, content). Keeps each widget focused on rendering its own data.
 */
function WidgetShell({
  title,
  icon: Icon,
  to,
  toLabel = "View all",
  isLoading,
  isError,
  onRetry,
  isEmpty,
  emptyIcon: EmptyIcon,
  emptyText = "Nothing to show yet",
  skeletonLines = 3,
  children,
}) {
  return (
    <section className="dw-card">
      <header className="dw-card-head">
        <h3>
          {Icon && <Icon size={16} />} {title}
        </h3>
        {to && (
          <Link to={to} className="dw-link">
            {toLabel} <ArrowRight size={14} />
          </Link>
        )}
      </header>

      <div className="dw-card-body">
        {isLoading ? (
          <div className="dw-skeleton">
            {Array.from({ length: skeletonLines }).map((_, i) => (
              <Skeleton key={i} height={14} style={{ marginBottom: 10, width: `${90 - i * 12}%` }} />
            ))}
          </div>
        ) : isError ? (
          <div className="dw-state dw-state-error">
            <AlertCircle size={20} />
            <span>Couldn't load this.</span>
            {onRetry && (
              <button type="button" className="dw-retry" onClick={onRetry}>
                <RefreshCw size={13} /> Retry
              </button>
            )}
          </div>
        ) : isEmpty ? (
          <div className="dw-state dw-state-empty">
            {EmptyIcon && <EmptyIcon size={22} />}
            <span>{emptyText}</span>
          </div>
        ) : (
          children
        )}
      </div>
    </section>
  );
}

export default memo(WidgetShell);
