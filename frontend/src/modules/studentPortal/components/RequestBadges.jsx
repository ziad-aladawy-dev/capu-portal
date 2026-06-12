import { useTranslation } from "react-i18next";
import PortalBadge from "./shared/PortalBadge";
import {
  REQUEST_STATUS_NAMES, REQUEST_STATUS_LABELS, REQUEST_STATUS_TONE,
  PAYMENT_STATUS_NAMES, PAYMENT_STATUS_LABELS, PAYMENT_STATUS_TONE,
} from "../constants/requestStatus";

/** Localized request-status badge (labels via t(status_<PascalName>)). */
export function RequestStatusBadge({ status }) {
  const { t } = useTranslation();
  if (status == null) return null;
  return (
    <PortalBadge tone={REQUEST_STATUS_TONE[status] || "neutral"}>
      {t(`portal_requests.status_${REQUEST_STATUS_NAMES[status]}`, {
        defaultValue: REQUEST_STATUS_LABELS[status] || String(status),
      })}
    </PortalBadge>
  );
}

/** Localized payment-status badge. */
export function PaymentStatusBadge({ status }) {
  const { t } = useTranslation();
  if (status == null) return null;
  return (
    <PortalBadge tone={PAYMENT_STATUS_TONE[status] || "neutral"}>
      {t(`portal_requests.payment_${PAYMENT_STATUS_NAMES[status]}`, {
        defaultValue: PAYMENT_STATUS_LABELS[status] || String(status),
      })}
    </PortalBadge>
  );
}
