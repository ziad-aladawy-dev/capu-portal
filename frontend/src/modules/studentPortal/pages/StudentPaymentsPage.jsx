import { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  Receipt, CheckCircle, AlertCircle, RefreshCw, Inbox, DollarSign,
  CreditCard, Lock, X, Loader2, ShoppingCart,
} from "lucide-react";
import { useAuth } from "../../../core/auth/useAuth";
import {
  FEE_STATUS, ORDER_STATUS, ORDER_STATUS_LABELS, ORDER_TERMINAL,
  GATEWAY, GATEWAY_LABELS, fmtAmount, feeLabel, initiateOrder,
} from "../../../core/services/treasuryPaymentService";
import {
  useStudentFees, useStudentOrders, useCreateOrder, useCancelOrder,
} from "../hooks/usePayments";
import { formatDate } from "../utils/format";
import "../styles/studentPayments.css";

const GATEWAYS = [GATEWAY.Mastercard, GATEWAY.BankMisr, GATEWAY.EFinance];

function StudentPaymentsPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const studentId = user?.id;

  const feesQ = useStudentFees(studentId);
  const ordersQ = useStudentOrders(studentId);
  const fees = useMemo(() => feesQ.data || [], [feesQ.data]);

  const [activeTab, setActiveTab] = useState(0);
  const [selected, setSelected] = useState({}); // feeId -> bool

  const pendingFees = useMemo(() => fees.filter((f) => f.status === FEE_STATUS.Pending), [fees]);
  const inOrderFees = useMemo(() => fees.filter((f) => f.status === FEE_STATUS.IncludedInOrder), [fees]);

  // Summary metrics
  const outstanding = pendingFees.reduce((s, f) => s + Number(f.totalAmount), 0);
  const inOrder = inOrderFees.reduce((s, f) => s + Number(f.totalAmount), 0);
  const paid = fees.filter((f) => f.status === FEE_STATUS.Paid).reduce((s, f) => s + Number(f.totalAmount), 0);

  const selectedFees = pendingFees.filter((f) => selected[f.id]);
  const selectedTotal = selectedFees.reduce((s, f) => s + Number(f.totalAmount), 0);

  function toggle(id) {
    setSelected((s) => ({ ...s, [id]: !s[id] }));
  }
  function toggleAll() {
    if (selectedFees.length === pendingFees.length) setSelected({});
    else setSelected(Object.fromEntries(pendingFees.map((f) => [f.id, true])));
  }

  if (feesQ.isLoading) {
    return (
      <div className="student-payments">
        <div className="sp-loading"><div className="spinner" /><p>{t("portal_payments.loading", { defaultValue: "Loading your fees…" })}</p></div>
      </div>
    );
  }

  const orders = ordersQ.data || [];
  const pendingOrders = orders.filter((o) => !ORDER_TERMINAL.includes(o.status));

  return (
    <div className="student-payments">
      <div className="sp-header">
        <h1>{t("portal_payments.title", { defaultValue: "Payments & Fees" })}</h1>
        <p>{t("portal_payments.subtitle", { defaultValue: "Pay your university fees through HU Treasury" })}</p>
      </div>

      <div className="sp-summary-grid">
        <SummaryCard variant="balance-due" icon={<DollarSign size={22} />} label={t("portal_payments.outstanding", { defaultValue: "Outstanding" })} value={outstanding} sub={`${pendingFees.length} ${t("portal_payments.fees_count", { defaultValue: "fee(s)" })}`} />
        <SummaryCard variant="discount" icon={<ShoppingCart size={22} />} label={t("portal_payments.in_order", { defaultValue: "In Open Orders" })} value={inOrder} sub={`${inOrderFees.length} ${t("portal_payments.fees_count", { defaultValue: "fee(s)" })}`} />
        <SummaryCard variant="total-paid" icon={<CheckCircle size={22} />} label={t("portal_payments.paid", { defaultValue: "Paid" })} value={paid} />
        <SummaryCard variant="total-fees" icon={<Receipt size={22} />} label={t("portal_payments.total", { defaultValue: "Total Fees" })} value={fees.reduce((s, f) => s + Number(f.totalAmount), 0)} sub={`${fees.length} ${t("portal_payments.fees_count", { defaultValue: "fee(s)" })}`} />
      </div>

      {(paid + outstanding + inOrder) > 0 && (
        <div className="sp-progress-card">
          <div className="sp-progress-head">
            <span>{t("portal_payments.payment_progress", { defaultValue: "Payment progress" })}</span>
            <strong>{Math.round((paid / (paid + outstanding + inOrder)) * 100)}% {t("portal_payments.paid_pct", { defaultValue: "paid" })}</strong>
          </div>
          <div className="sp-progress-track">
            <div className="sp-progress-fill" style={{ width: `${(paid / (paid + outstanding + inOrder)) * 100}%` }} />
          </div>
        </div>
      )}

      <div className="sp-tabs-bar">
        <button className={`sp-tab${activeTab === 0 ? " active" : ""}`} onClick={() => setActiveTab(0)}>
          {t("portal_payments.tab_pay", { defaultValue: "Pay Fees" })}
          {pendingFees.length > 0 && <span className="sp-tab-badge urgent">{pendingFees.length}</span>}
        </button>
        <button className={`sp-tab${activeTab === 1 ? " active" : ""}`} onClick={() => setActiveTab(1)}>
          {t("portal_payments.tab_orders", { defaultValue: "Orders" })}
          {pendingOrders.length > 0 && <span className="sp-tab-badge">{pendingOrders.length}</span>}
        </button>
      </div>

      {activeTab === 0 ? (
        <PayFeesTab
          studentId={studentId}
          pendingFees={pendingFees}
          inOrderFees={inOrderFees}
          selected={selected}
          toggle={toggle}
          toggleAll={toggleAll}
          allSelected={pendingFees.length > 0 && selectedFees.length === pendingFees.length}
          selectedFees={selectedFees}
          selectedTotal={selectedTotal}
          refreshing={feesQ.isFetching}
          onRecheck={() => { feesQ.refetch(); ordersQ.refetch(); }}
          isError={feesQ.isError}
        />
      ) : (
        <OrdersTab studentId={studentId} orders={orders} isError={ordersQ.isError} isLoading={ordersQ.isLoading} />
      )}
    </div>
  );
}

function SummaryCard({ variant, icon, label, value, sub }) {
  const { t } = useTranslation();
  return (
    <div className={`sp-summary-card ${variant}`}>
      <div className={`sp-card-icon ${variant}`}>{icon}</div>
      <div className="sp-card-info">
        <div className="sp-card-label">{label}</div>
        <div className="sp-card-value">{fmtAmount(value)}<span className="currency">{t("egp", { defaultValue: "EGP" })}</span></div>
        {sub && <div className="sp-card-sub">{sub}</div>}
      </div>
    </div>
  );
}

// ── Pay Fees tab ─────────────────────────────────────────────────────────────
function PayFeesTab({
  studentId, pendingFees, inOrderFees, selected, toggle, toggleAll, allSelected,
  selectedFees, selectedTotal, refreshing, onRecheck, isError,
}) {
  const { t } = useTranslation();
  const [showGateways, setShowGateways] = useState(false);

  if (isError) {
    return (
      <div className="sp-error">
        <AlertCircle size={36} color="#dc2626" />
        <h3>{t("portal_payments.load_error", { defaultValue: "Couldn't load your fees" })}</h3>
        <p>{t("portal_payments.load_error_hint", { defaultValue: "Please try again in a moment." })}</p>
        <button className="sp-retry-btn" onClick={onRecheck}>{t("retry", { defaultValue: "Retry" })}</button>
      </div>
    );
  }

  const nothing = pendingFees.length === 0 && inOrderFees.length === 0;

  return (
    <>
      <div className="sp-toolbar">
        <button className="sp-recheck-btn" onClick={onRecheck} disabled={refreshing}>
          <RefreshCw size={14} className={refreshing ? "spinning" : ""} />
          {t("portal_payments.recheck", { defaultValue: "Refresh" })}
        </button>
      </div>

      {nothing ? (
        <div className="sp-empty-state">
          <CheckCircle size={40} />
          <h3>{t("portal_payments.all_clear", { defaultValue: "Nothing to pay" })}</h3>
          <p>{t("portal_payments.all_clear_hint", { defaultValue: "You have no outstanding fees right now." })}</p>
        </div>
      ) : (
        <div className="sp-table-wrapper">
          <table className="sp-table">
            <thead>
              <tr>
                <th className="sp-select-col">
                  {pendingFees.length > 0 && (
                    <input type="checkbox" checked={allSelected} onChange={toggleAll} aria-label={t("portal_payments.select_all", { defaultValue: "Select all" })} />
                  )}
                </th>
                <th>{t("portal_payments.fee", { defaultValue: "Fee" })}</th>
                <th>{t("portal_payments.source", { defaultValue: "Source" })}</th>
                <th className="text-right">{t("portal_payments.unit", { defaultValue: "Unit" })}</th>
                <th className="text-center">{t("portal_payments.qty", { defaultValue: "Qty" })}</th>
                <th className="text-right">{t("portal_payments.amount", { defaultValue: "Amount" })}</th>
                <th className="text-center">{t("portal_payments.status", { defaultValue: "Status" })}</th>
              </tr>
            </thead>
            <tbody>
              {pendingFees.map((f) => (
                <tr key={f.id} className={selected[f.id] ? "sp-row-selected" : ""}>
                  <td className="sp-select-col">
                    <input type="checkbox" checked={!!selected[f.id]} onChange={() => toggle(f.id)} aria-label={`select ${feeLabel(f)}`} />
                  </td>
                  <td><span className="sp-fee-title">{feeLabel(f)}</span></td>
                  <td>{f.sourceModule || <span className="sp-dash">–</span>}</td>
                  <td className="text-right">{fmtAmount(f.unitAmount)}</td>
                  <td className="text-center">{f.quantity}</td>
                  <td className="text-right"><span className="sp-amount remaining">{fmtAmount(f.totalAmount)}</span></td>
                  <td className="text-center"><span className="sp-status-badge unpaid">{t("portal_payments.pending", { defaultValue: "Pending" })}</span></td>
                </tr>
              ))}
              {inOrderFees.map((f) => (
                <tr key={f.id} className="sp-row-locked">
                  <td className="sp-select-col"><Lock size={13} className="sp-lock-icon" /></td>
                  <td><span className="sp-fee-title">{feeLabel(f)}</span></td>
                  <td>{f.sourceModule || <span className="sp-dash">–</span>}</td>
                  <td className="text-right">{fmtAmount(f.unitAmount)}</td>
                  <td className="text-center">{f.quantity}</td>
                  <td className="text-right"><span className="sp-amount">{fmtAmount(f.totalAmount)}</span></td>
                  <td className="text-center"><span className="sp-status-badge partial">{t("portal_payments.in_order_short", { defaultValue: "In Order" })}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {selectedFees.length > 0 && (
        <div className="sp-pay-bar">
          <div className="sp-pay-bar-info">
            <span>{selectedFees.length} {t("portal_payments.selected", { defaultValue: "selected" })}</span>
            <strong>{fmtAmount(selectedTotal)} {t("egp", { defaultValue: "EGP" })}</strong>
          </div>
          <button className="sp-pay-btn" onClick={() => setShowGateways(true)}>
            <CreditCard size={16} /> {t("portal_payments.pay_selected", { defaultValue: "Pay Selected" })}
          </button>
        </div>
      )}

      {showGateways && (
        <GatewayModal
          studentId={studentId}
          feeIds={selectedFees.map((f) => f.id)}
          total={selectedTotal}
          onClose={() => setShowGateways(false)}
        />
      )}
    </>
  );
}

// ── Gateway picker + checkout ────────────────────────────────────────────────
function GatewayModal({ studentId, feeIds, total, onClose }) {
  const { t } = useTranslation();
  const createOrder = useCreateOrder();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  async function pay(gateway) {
    setError(null);
    setBusy(true);
    try {
      const order = await createOrder.mutateAsync({ studentId, feeIds, gateway });
      const returnUrl = `${window.location.origin}/student/payments/return?orderId=${order.id}`;
      const session = await initiateOrder(order.id, returnUrl);
      // Stash so the return page can recover the order even if the gateway
      // drops our query param.
      localStorage.setItem("capu_pending_order", order.id);
      if (!session?.redirectUrl) throw new Error("no redirect url");
      window.location.assign(session.redirectUrl);
    } catch (err) {
      const status = err?.response?.status;
      setError(
        status === 403
          ? t("portal_payments.pay_forbidden", { defaultValue: "You don't have permission to pay these fees." })
          : t("portal_payments.pay_error", { defaultValue: "Couldn't start the payment. Please try again." })
      );
      setBusy(false);
    }
  }

  return (
    <div className="sp-modal-overlay" onClick={busy ? undefined : onClose}>
      <div className="sp-modal" onClick={(e) => e.stopPropagation()}>
        <div className="sp-modal-head">
          <h3>{t("portal_payments.choose_gateway", { defaultValue: "Choose a payment method" })}</h3>
          <button className="sp-modal-close" onClick={onClose} disabled={busy}><X size={18} /></button>
        </div>
        <p className="sp-modal-total">{t("portal_payments.total_due", { defaultValue: "Total" })}: <strong>{fmtAmount(total)} {t("egp", { defaultValue: "EGP" })}</strong></p>
        {error && <div className="sp-modal-error"><AlertCircle size={15} /> {error}</div>}
        <div className="sp-gateway-list">
          {GATEWAYS.map((g) => (
            <button key={g} className="sp-gateway-option" onClick={() => pay(g)} disabled={busy}>
              <CreditCard size={18} />
              <span>{GATEWAY_LABELS[g]}</span>
              {busy ? <Loader2 size={15} className="sp-spin" /> : null}
            </button>
          ))}
        </div>
        <p className="sp-modal-note">{t("portal_payments.redirect_note", { defaultValue: "You'll be redirected to the secure HU Treasury payment page." })}</p>
      </div>
    </div>
  );
}

// ── Orders tab ───────────────────────────────────────────────────────────────
function OrdersTab({ studentId, orders, isError, isLoading }) {
  const { t } = useTranslation();
  if (isLoading) return <div className="sp-empty-state"><Loader2 size={32} className="sp-spin" /><p>{t("loading", { defaultValue: "Loading…" })}</p></div>;
  if (isError) return <div className="sp-error"><AlertCircle size={36} color="#dc2626" /><h3>{t("portal_payments.orders_error", { defaultValue: "Couldn't load your orders" })}</h3></div>;
  if (orders.length === 0) {
    return <div className="sp-empty-state"><Inbox size={40} /><h3>{t("portal_payments.no_orders", { defaultValue: "No orders yet" })}</h3><p>{t("portal_payments.no_orders_hint", { defaultValue: "Your payment orders will appear here." })}</p></div>;
  }
  return (
    <div className="sp-orders">
      {orders.map((o) => <OrderCard key={o.id} order={o} studentId={studentId} />)}
    </div>
  );
}

function OrderCard({ order, studentId }) {
  const { t, i18n } = useTranslation();
  const cancelOrder = useCancelOrder(studentId);
  const [confirming, setConfirming] = useState(false);
  const [resuming, setResuming] = useState(false);
  const [error, setError] = useState(null);

  const statusCls =
    order.status === ORDER_STATUS.Paid ? "paid"
      : order.status === ORDER_STATUS.Failed || order.status === ORDER_STATUS.Expired ? "unpaid"
        : order.status === ORDER_STATUS.Cancelled || order.status === ORDER_STATUS.Refunded ? "partial"
          : "pending";
  const canAct = !ORDER_TERMINAL.includes(order.status);

  async function resume() {
    setError(null);
    setResuming(true);
    try {
      const returnUrl = `${window.location.origin}/student/payments/return?orderId=${order.id}`;
      const session = await initiateOrder(order.id, returnUrl);
      localStorage.setItem("capu_pending_order", order.id);
      if (!session?.redirectUrl) throw new Error("no redirect url");
      window.location.assign(session.redirectUrl);
    } catch {
      setError(t("portal_payments.resume_error", { defaultValue: "Couldn't resume payment." }));
      setResuming(false);
    }
  }

  return (
    <div className="sp-order-card">
      <div className="sp-order-main">
        <div>
          <div className="sp-order-id">#{order.merchantOrderId || order.id.slice(0, 8)}</div>
          <div className="sp-order-meta">
            {GATEWAY_LABELS[order.gateway]} · {(order.fees || []).length} {t("portal_payments.fees_count", { defaultValue: "fee(s)" })} · {formatDate(order.createdAt, i18n.language)}
          </div>
        </div>
        <div className="sp-order-right">
          <div className="sp-order-total">{fmtAmount(order.totalAmount)} {t("egp", { defaultValue: "EGP" })}</div>
          <span className={`sp-status-badge ${statusCls}`}>
            {t(`portal_payments.order_status_${order.status}`, { defaultValue: ORDER_STATUS_LABELS[order.status] })}
          </span>
        </div>
      </div>
      {error && <div className="sp-modal-error"><AlertCircle size={14} /> {error}</div>}
      {canAct && (
        <div className="sp-order-actions">
          <button className="sp-pay-btn small" onClick={resume} disabled={resuming || cancelOrder.isPending}>
            {resuming ? <Loader2 size={14} className="sp-spin" /> : <CreditCard size={14} />}
            {t("portal_payments.continue_payment", { defaultValue: "Continue Payment" })}
          </button>
          {confirming ? (
            <span className="sp-confirm-inline">
              {t("portal_payments.cancel_confirm", { defaultValue: "Cancel this order?" })}
              <button className="sp-link danger" onClick={() => cancelOrder.mutate(order.id)} disabled={cancelOrder.isPending}>
                {t("yes", { defaultValue: "Yes" })}
              </button>
              <button className="sp-link" onClick={() => setConfirming(false)}>{t("no", { defaultValue: "No" })}</button>
            </span>
          ) : (
            <button className="sp-link danger" onClick={() => setConfirming(true)} disabled={cancelOrder.isPending}>
              {t("portal_payments.cancel_order", { defaultValue: "Cancel Order" })}
            </button>
          )}
        </div>
      )}
    </div>
  );
}

export default StudentPaymentsPage;
