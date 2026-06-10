import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  ExternalLink, Copy, Check, RadioTower, ShieldCheck, RotateCcw,
  AlertTriangle, Ban, Hourglass,
} from "lucide-react";
import Drawer from "../../../core/components/Drawer";
import StatusBadge from "../../../core/components/StatusBadge";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import { useToast } from "../../../core/components/Toast";
import {
  useOrder, useCancelOrder, useInitiatePayment, useInvalidateTreasury,
} from "../../../core/query/useTreasury";
import {
  ORDER_STATUS, ORDER_STATUS_KEYS, ORDER_STATUS_BADGE,
  FEE_STATUS_KEYS, GATEWAY_KEYS, fmtAmount, apiErrorMessage,
} from "../../../core/services/treasuryService";

function useCopy() {
  const { addToast } = useToast();
  const { t } = useTranslation();
  const [copiedKey, setCopiedKey] = useState(null);
  const copy = async (text, key) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopiedKey(key);
      setTimeout(() => setCopiedKey(null), 1800);
    } catch {
      addToast(t("treasury_copy_failed"), "error");
    }
  };
  return { copy, copiedKey };
}

/**
 * Order lifecycle panel. Shows the fee breakdown and drives every state
 * transition an employee can perform:
 *   Created        → initiate (get gateway link) or cancel (release fees)
 *   PendingPayment → share/open the payment link; status auto-polls until
 *                    the webhook or reconciliation job settles the order
 *   Paid           → settled summary
 *   Failed/Expired → explains that fees returned to the outstanding pool
 */
export default function OrderDetailDrawer({
  orderId,
  open,
  onClose,
  receiptIndex = {},
  canTransact = false,
}) {
  const { t } = useTranslation();
  const { addToast } = useToast();
  const { data: order, isLoading, error } = useOrder(open ? orderId : null);
  const initiate = useInitiatePayment();
  const cancel = useCancelOrder();
  const invalidateTreasury = useInvalidateTreasury();
  const { copy, copiedKey } = useCopy();
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [actionError, setActionError] = useState("");

  // Announce settlement outcomes the moment polling observes them. The ref
  // tracks (orderId, status) so switching orders never reads a stale status.
  const prevRef = useRef({ orderId: null, status: null });
  useEffect(() => {
    if (!order) return;
    const prev = prevRef.current;
    prevRef.current = { orderId: order.id, status: order.status };
    if (prev.orderId !== order.id || prev.status === order.status) return;
    if (order.status === ORDER_STATUS.Paid) {
      addToast(t("treasury_payment_received_toast"), "success");
      invalidateTreasury();
    } else if (order.status === ORDER_STATUS.Failed) {
      addToast(t("treasury_payment_failed_toast"), "warning");
      invalidateTreasury();
    } else if (order.status === ORDER_STATUS.Expired) {
      addToast(t("treasury_payment_expired_toast"), "warning");
      invalidateTreasury();
    }
  }, [order, addToast, t, invalidateTreasury]);

  const handleClose = () => {
    setActionError("");
    setConfirmCancel(false);
    onClose();
  };

  const handleInitiate = async () => {
    setActionError("");
    try {
      const result = await initiate.mutateAsync({ id: orderId });
      if (result?.redirectUrl) {
        addToast(t("treasury_initiated_toast"), "success");
      }
    } catch (err) {
      setActionError(apiErrorMessage(err, t("treasury_initiate_failed")));
    }
  };

  const handleCancel = async () => {
    setActionError("");
    try {
      await cancel.mutateAsync(orderId);
      setConfirmCancel(false);
      addToast(t("treasury_order_cancelled_toast"), "info");
    } catch (err) {
      setConfirmCancel(false);
      setActionError(apiErrorMessage(err, t("treasury_cancel_failed")));
    }
  };

  const status = order?.status;
  const isCreated = status === ORDER_STATUS.Created;
  const isPending = status === ORDER_STATUS.PendingPayment;
  const isPaid = status === ORDER_STATUS.Paid;
  const isReleased = status === ORDER_STATUS.Failed || status === ORDER_STATUS.Expired;
  const busy = initiate.isPending || cancel.isPending;

  return (
    <>
      <Drawer
        open={open}
        onClose={handleClose}
        title={t("treasury_order_details")}
        width={520}
        loading={busy}
        footer={
          canTransact && order && isCreated ? (
            <>
              <button
                className="ty-btn ty-btn-danger-ghost"
                onClick={() => setConfirmCancel(true)}
                disabled={busy}
              >
                <Ban size={14} aria-hidden="true" /> {t("treasury_cancel_order")}
              </button>
              <button
                className="ty-btn ty-btn-primary"
                onClick={handleInitiate}
                disabled={busy}
              >
                <RadioTower size={14} aria-hidden="true" />
                {initiate.isPending ? t("treasury_initiating") : t("treasury_initiate_payment")}
              </button>
            </>
          ) : null
        }
      >
        {isLoading && <div className="ty-drawer-loading">{t("loading")}…</div>}
        {error && (
          <div className="ty-alert ty-alert-error" role="alert">
            {apiErrorMessage(error, t("treasury_order_load_failed"))}
          </div>
        )}

        {order && (
          <div className="ty-order-detail">
            <div className="ty-order-detail-head">
              <div className="ty-order-detail-amount">
                {fmtAmount(order.totalAmount)}
                <span className="ty-currency">{order.currency}</span>
              </div>
              <div className="ty-order-detail-meta">
                <StatusBadge
                  status={ORDER_STATUS_BADGE[order.status]}
                  label={t(ORDER_STATUS_KEYS[order.status])}
                />
                <span className="ty-meta-chip">{t(GATEWAY_KEYS[order.gateway])}</span>
                <span className="ty-meta-chip">
                  {new Date(order.createdAt).toLocaleString()}
                </span>
              </div>
            </div>

            {actionError && (
              <div className="ty-alert ty-alert-error" role="alert">
                {actionError}
              </div>
            )}

            {isCreated && (
              <div className="ty-alert ty-alert-info">
                <Hourglass size={15} aria-hidden="true" />
                <div>
                  <strong>{t("treasury_order_created_title")}</strong>
                  <p>{t("treasury_order_created_hint")}</p>
                </div>
              </div>
            )}

            {isPending && (
              <div className="ty-paylink-panel">
                <div className="ty-paylink-live">
                  <span className="ty-live-dot" aria-hidden="true" />
                  {t("treasury_awaiting_payment_live")}
                </div>
                {order.redirectUrl ? (
                  <div className="ty-paylink-actions">
                    <a
                      className="ty-btn ty-btn-primary"
                      href={order.redirectUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      <ExternalLink size={14} aria-hidden="true" />
                      {t("treasury_open_payment_page")}
                    </a>
                    <button
                      className="ty-btn"
                      onClick={() => copy(order.redirectUrl, "link")}
                    >
                      {copiedKey === "link"
                        ? <Check size={14} aria-hidden="true" />
                        : <Copy size={14} aria-hidden="true" />}
                      {copiedKey === "link" ? t("treasury_copied") : t("treasury_copy_payment_link")}
                    </button>
                  </div>
                ) : (
                  canTransact && (
                    <button
                      className="ty-btn ty-btn-primary"
                      onClick={handleInitiate}
                      disabled={busy}
                    >
                      <RotateCcw size={14} aria-hidden="true" />
                      {t("treasury_recover_payment_link")}
                    </button>
                  )
                )}
                <p className="ty-paylink-note">{t("treasury_payment_link_expiry_note")}</p>
              </div>
            )}

            {isPaid && (
              <div className="ty-alert ty-alert-success">
                <ShieldCheck size={15} aria-hidden="true" />
                <div>
                  <strong>{t("treasury_order_paid_title")}</strong>
                  <p>{t("treasury_order_paid_hint")}</p>
                </div>
              </div>
            )}

            {isReleased && (
              <div className="ty-alert ty-alert-warn">
                <AlertTriangle size={15} aria-hidden="true" />
                <div>
                  <strong>
                    {status === ORDER_STATUS.Failed
                      ? t("treasury_order_failed_title")
                      : t("treasury_order_expired_title")}
                  </strong>
                  <p>{t("treasury_order_released_hint")}</p>
                </div>
              </div>
            )}

            <h3 className="ty-section-title">
              {t("treasury_fees_in_order", { count: order.fees?.length ?? 0 })}
            </h3>
            <div className="ty-order-fees">
              {(order.fees || []).map((fee) => {
                const receipt = receiptIndex[fee.receiptId];
                return (
                  <div key={fee.id} className="ty-order-fee-row">
                    <div className="ty-order-fee-main">
                      <span className="ty-order-fee-name">
                        {receipt?.name || t("treasury_fee_fallback_name")}
                      </span>
                      <span className="ty-order-fee-sub">
                        {fee.quantity} × {fmtAmount(fee.unitAmount)} {fee.currency}
                        {fee.sourceModule && (
                          <span className="ty-meta-chip">{fee.sourceModule}</span>
                        )}
                      </span>
                    </div>
                    <div className="ty-order-fee-side">
                      <span className="ty-order-fee-amount">
                        {fmtAmount(fee.totalAmount)}
                      </span>
                      <StatusBadge
                        status={fee.status === 2 ? "active" : fee.status === 1 ? "open" : "inactive"}
                        label={t(FEE_STATUS_KEYS[fee.status])}
                      />
                    </div>
                  </div>
                );
              })}
            </div>

            <div className="ty-order-refs">
              <RefLine
                label={t("treasury_order_id")}
                value={order.id}
                copied={copiedKey === "orderId"}
                onCopy={() => copy(order.id, "orderId")}
              />
              {order.merchantOrderId && (
                <RefLine
                  label={t("treasury_merchant_ref")}
                  value={order.merchantOrderId}
                  copied={copiedKey === "merchantRef"}
                  onCopy={() => copy(order.merchantOrderId, "merchantRef")}
                />
              )}
            </div>
          </div>
        )}
      </Drawer>

      <ConfirmDialog
        open={confirmCancel}
        onClose={() => setConfirmCancel(false)}
        onConfirm={handleCancel}
        title={t("treasury_cancel_order")}
        message={t("treasury_cancel_order_confirm")}
        detail={t("treasury_cancel_order_detail")}
        confirmLabel={t("treasury_cancel_order")}
        cancelLabel={t("keep")}
        variant="danger"
        loading={cancel.isPending}
      />
    </>
  );
}

function RefLine({ label, value, copied, onCopy }) {
  return (
    <div className="ty-ref-line">
      <span className="ty-ref-label">{label}</span>
      <span className="ty-ref-value" title={value}>{value}</span>
      <button
        className="ty-icon-btn"
        onClick={onCopy}
        aria-label={`Copy ${label}`}
      >
        {copied ? <Check size={13} /> : <Copy size={13} />}
      </button>
    </div>
  );
}
