import { memo } from "react";
import { useTranslation } from "react-i18next";
import { AlertCircle, RefreshCw } from "lucide-react";
import PortalCard from "../shared/PortalCard";
import PortalSectionHeader from "../shared/PortalSectionHeader";
import PortalSkeleton from "../shared/PortalSkeleton";
import PortalEmptyState from "../shared/PortalEmptyState";

/**
 * Standard chrome for every dashboard widget: title bar, optional "view all"
 * link, and the four canonical async states (loading skeleton, error+retry,
 * empty, content). Keeps each widget focused on rendering its own data.
 */
function WidgetShell({
  title,
  icon: Icon,
  to,
  toLabel,
  isLoading,
  isError,
  onRetry,
  isEmpty,
  emptyIcon,
  emptyText,
  emptyAction,
  skeletonLines = 3,
  children,
}) {
  const { t } = useTranslation();
  return (
    <PortalCard className="dw-card">
      <PortalSectionHeader
        icon={Icon}
        title={title}
        to={to}
        toLabel={toLabel ?? t("portal_dashboard.view_all", { defaultValue: "View all" })}
      />

      <div className="dw-card-body">
        {isLoading ? (
          <PortalSkeleton.Lines count={skeletonLines} />
        ) : isError ? (
          <PortalEmptyState
            compact
            icon={AlertCircle}
            text={t("portal_dashboard.load_failed", { defaultValue: "Couldn't load this." })}
            onAction={onRetry}
            actionLabel={onRetry ? (
              <><RefreshCw size={12} /> {t("retry", { defaultValue: "Retry" })}</>
            ) : undefined}
          />
        ) : isEmpty ? (
          <PortalEmptyState
            compact
            icon={emptyIcon}
            text={emptyText ?? t("portal_dashboard.nothing_yet", { defaultValue: "Nothing to show yet" })}
            action={emptyAction}
          />
        ) : (
          children
        )}
      </div>
    </PortalCard>
  );
}

export default memo(WidgetShell);
