import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  Receipt, CheckCircle, AlertCircle, RefreshCw, Inbox, DollarSign,
  CreditCard, Hourglass, ExternalLink,
} from "lucide-react";
import { useAuth } from "../../../core/auth/useAuth";
import { useCanDo } from "../../../core/auth/usePermission";
import { useToast } from "../../../core/components/Toast";
import {
  useUnpaidFees, useStudentOrders, useReceipts, buildReceiptIndex,
  useCreateOrder, useInitiatePayment,
} from "../../../core/query/useTreasury";
import {
  ORDER_STATUS, ORDER_STATUS_KEYS, GATEWAY, GATEWAY_KEYS,
  fmtAmount, apiErrorMessage,
} from "../../../core/services/treasuryService";
import "../styles/studentPayments.css";

const TABS = ["unpaid_fees_tab", "treasury_orders_tab", "payment_history_tab"];
const EMPTY_LIST = [];

function sumByCurrency(rows, amountOf) {
  const map = {};
  for (const row of rows) {
    const c = row.currency || "EGP";
    map[c] = (map[c] || 0) + Number(amountOf(row));
  }
  return Object.entries(map).map(([currency, total]) => ({ currency, total }));
}

function MoneySums({ sums }) {
  if (!sums.length) {
    return (
      <>
        0.00<span className="currency">EGP</span>
      </>
    );
  }
  return sums.map((s, i) => (
    <span key={s.currency}>
      {i > 0 && " · "}
      {fmtAmount(s.total)}
      <span className="currency">{s.currency}</span>
    </span>
  ));
}

function orderBadgeClass(status) {
  if (status === ORDER_STATUS.Paid) return "paid";
  if (status === ORDER_STATUS.PendingPayment || status === ORDER_STATUS.Created) return "partial";
  return "unpaid";
}

function StudentPaymentsPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { addToast } = useToast();
  const canPay = useCanDo("payments.transactions", 2);
  const [activeTab, setActiveTab] = useState(0);

  const fees = useUnpaidFees(user?.id);
  const orders = useStudentOrders(user?.id);
  const { data: receipts = [] } = useReceipts();
  const receiptIndex = useMemo(() => buildReceiptIndex(receipts), [receipts]);

  const feeRows = fees.data || EMPTY_LIST;
  const orderRows = orders.data || EMPTY_LIST;

  const paidOrders = orderRows.filter((o) => o.status === ORDER_STATUS.Paid);
  const openOrders = orderRows.filter(
    (o) => o.status === ORDER_STATUS.PendingPayment || o.status === ORDER_STATUS.Created
  );

  const outstandingSums = useMemo(() => sumByCurrency(feeRows, (f) => f.totalAmount), [feeRows]);
  const inFlightSums = useMemo(() => sumByCurrency(openOrders, (o) => o.totalAmount), [openOrders]);
  const paidSums = useMemo(() => sumByCurrency(paidOrders, (o) => o.totalAmount), [paidOrders]);

  /* Paid fees flattened from settled orders — the payment history. */
  const historyRows = useMemo(() => {
    const rows = [];
    for (const order of paidOrders) {
      for (const fee of order.fees || []) {
        rows.push({ order, fee });
      }
    }
    return rows;
  }, [paidOrders]);

  const loading = (fees.isLoading || orders.isLoading) && !fees.data && !orders.data;
  const fatalError = fees.error && orders.error;
  const refreshing = fees.isFetching || orders.isFetching;

  const recheck = () => {
    fees.refetch();
    orders.refetch();
  };

  const feeName = (fee) =>
    receiptIndex[fee.receiptId]?.name || t("treasury_fee_fallback_name");

  if (loading) {
    return (
      <div className="student-payments">
        <div className="sp-loading">
          <div className="spinner"></div>
          <p>{t("loading_payments")}</p>
        </div>
      </div>
    );
  }

  if (fatalError) {
    return (
      <div className="student-payments">
        <div className="sp-header">
          <h1>{t("payments_and_fees")}</h1>
          <p>{t("payments_subtitle")}</p>
        </div>
        <div className="sp-error">
          <AlertCircle size={36} color="#dc2626" />
          <h3>{t("failed_to_load_data")}</h3>
          <p>{apiErrorMessage(fees.error, "")}</p>
          <button className="sp-retry-btn" onClick={recheck}>
            {t("retry")}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="student-payments">
      <div className="sp-header">
        <h1>{t("payments_and_fees")}</h1>
        <p>{t("payments_subtitle")}</p>
      </div>

      {/* Summary cards */}
      <div className="sp-summary-grid">
        <div className="sp-summary-card total-fees">
          <div className="sp-card-icon total-fees">
            <Receipt size={22} />
          </div>
          <div className="sp-card-info">
            <div className="sp-card-label">{t("balance_due")}</div>
            <div className="sp-card-value">
              <MoneySums sums={outstandingSums} />
            </div>
            <div className="sp-card-sub">
              {t("treasury_fees_count", { count: feeRows.length })}
            </div>
          </div>
        </div>

        <div className="sp-summary-card discount">
          <div className="sp-card-icon discount">
            <Hourglass size={22} />
          </div>
          <div className="sp-card-info">
            <div className="sp-card-label">{t("treasury_awaiting_payment")}</div>
            <div className="sp-card-value">
              <MoneySums sums={inFlightSums} />
            </div>
            <div className="sp-card-sub">
              {t("treasury_orders_count", { count: openOrders.length })}
            </div>
          </div>
        </div>

        <div className="sp-summary-card total-paid">
          <div className="sp-card-icon total-paid">
            <CheckCircle size={22} />
          </div>
          <div className="sp-card-info">
            <div className="sp-card-label">{t("total_paid")}</div>
            <div className="sp-card-value">
              <MoneySums sums={paidSums} />
            </div>
            <div className="sp-card-sub">
              {t("treasury_orders_count", { count: paidOrders.length })}
            </div>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="sp-tabs-bar">
        {TABS.map((label, idx) => {
          let badge = null;
          if (idx === 0 && feeRows.length > 0) {
            badge = <span className="sp-tab-badge urgent">{feeRows.length}</span>;
          }
          if (idx === 1 && openOrders.length > 0) {
            badge = <span className="sp-tab-badge">{openOrders.length}</span>;
          }
          return (
            <button
              key={label}
              className={`sp-tab${activeTab === idx ? " active" : ""}`}
              onClick={() => setActiveTab(idx)}
            >
              {t(label)}
              {badge}
            </button>
          );
        })}
      </div>

      {activeTab === 0 && (
        <UnpaidFeesTab
          fees={feeRows}
          feeName={feeName}
          canPay={canPay}
          studentId={user?.id}
          refreshing={refreshing}
          onRecheck={recheck}
          addToast={addToast}
        />
      )}
      {activeTab === 1 && (
        <OrdersTab orders={orderRows} refreshing={refreshing} onRecheck={recheck} />
      )}
      {activeTab === 2 && <HistoryTab rows={historyRows} feeName={feeName} />}
    </div>
  );
}

/* ════════════════════════════════════════════════════════════════
   Outstanding fees + optional self-pay
   ════════════════════════════════════════════════════════════════ */

function UnpaidFeesTab({ fees, feeName, canPay, studentId, refreshing, onRecheck, addToast }) {
  const { t } = useTranslation();
  const [selectedIds, setSelectedIds] = useState(new Set());
  const [gateway, setGateway] = useState(GATEWAY.Mastercard);
  const [payError, setPayError] = useState("");
  const createOrder = useCreateOrder();
  const initiate = useInitiatePayment();

  const selected = fees.filter((f) => selectedIds.has(f.id));
  const activeCurrency = selected[0]?.currency ?? null;
  const selectedTotal = selected.reduce((s, f) => s + Number(f.totalAmount), 0);
  const paying = createOrder.isPending || initiate.isPending;

  const toggle = (fee) => {
    if (activeCurrency && fee.currency !== activeCurrency && !selectedIds.has(fee.id)) return;
    setPayError("");
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(fee.id)) next.delete(fee.id);
      else next.add(fee.id);
      return next;
    });
  };

  const payNow = async () => {
    if (!selected.length) return;
    setPayError("");
    try {
      const order = await createOrder.mutateAsync({
        studentId,
        feeIds: [...selectedIds],
        gateway: Number(gateway),
      });
      const result = await initiate.mutateAsync({
        id: order.id,
        redirectUrl: window.location.href,
      });
      if (result?.redirectUrl) {
        addToast(t("treasury_redirecting_to_gateway"), "info");
        window.location.assign(result.redirectUrl);
      } else {
        setPayError(t("treasury_initiate_failed"));
      }
    } catch (err) {
      setPayError(apiErrorMessage(err, t("treasury_initiate_failed")));
      setSelectedIds(new Set());
    }
  };

  return (
    <>
      <div className="sp-toolbar">
        <button className="sp-recheck-btn" onClick={onRecheck} disabled={refreshing}>
          <RefreshCw size={14} className={refreshing ? "spinning" : ""} />
          {t("recheck_payments")}
        </button>

        {canPay && fees.length > 0 && (
          <div className="sp-pay-bar">
            <select
              className="sp-gateway-select"
              value={gateway}
              onChange={(e) => setGateway(e.target.value)}
              aria-label={t("treasury_choose_gateway")}
            >
              {Object.values(GATEWAY).map((g) => (
                <option key={g} value={g}>
                  {t(GATEWAY_KEYS[g])}
                </option>
              ))}
            </select>
            <button
              className="sp-pay-btn"
              onClick={payNow}
              disabled={!selected.length || paying}
            >
              <CreditCard size={14} />
              {paying
                ? t("treasury_redirecting_to_gateway")
                : selected.length
                ? `${t("treasury_pay_selected")} — ${fmtAmount(selectedTotal)} ${activeCurrency}`
                : t("treasury_pay_selected")}
            </button>
          </div>
        )}
      </div>

      {payError && (
        <div className="sp-inline-error" role="alert">
          <AlertCircle size={14} /> {payError}
        </div>
      )}

      {fees.length === 0 ? (
        <div className="sp-empty-state">
          <CheckCircle size={40} />
          <h3>{t("all_fees_paid_title")}</h3>
          <p>{t("all_fees_paid_message")}</p>
        </div>
      ) : (
        <div className="sp-table-wrapper">
          <table className="sp-table">
            <thead>
              <tr>
                {canPay && <th style={{ width: 36 }}></th>}
                <th>{t("fee_title")}</th>
                <th>{t("treasury_source")}</th>
                <th>{t("date")}</th>
                <th className="text-right">{t("treasury_quantity")}</th>
                <th className="text-right">{t("treasury_unit_price")}</th>
                <th className="text-right">{t("amount")}</th>
              </tr>
            </thead>
            <tbody>
              {fees.map((fee) => {
                const checked = selectedIds.has(fee.id);
                const locked = activeCurrency && fee.currency !== activeCurrency && !checked;
                return (
                  <tr
                    key={fee.id}
                    style={locked ? { opacity: 0.5 } : undefined}
                    title={
                      locked
                        ? t("treasury_currency_locked_hint", { currency: activeCurrency })
                        : undefined
                    }
                  >
                    {canPay && (
                      <td className="text-center">
                        <input
                          type="checkbox"
                          checked={checked}
                          disabled={locked || paying}
                          onChange={() => toggle(fee)}
                          aria-label={feeName(fee)}
                        />
                      </td>
                    )}
                    <td><span className="sp-fee-title">{feeName(fee)}</span></td>
                    <td>{fee.sourceModule || <span className="sp-dash">–</span>}</td>
                    <td>{new Date(fee.createdAt).toLocaleDateString()}</td>
                    <td className="text-right">{fee.quantity}</td>
                    <td className="text-right">{fmtAmount(fee.unitAmount)}</td>
                    <td className="text-right">
                      <span className="sp-amount remaining">
                        {fmtAmount(fee.totalAmount)} {fee.currency}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {!canPay && fees.length > 0 && (
        <p className="sp-footnote">{t("treasury_pay_at_office_hint")}</p>
      )}
    </>
  );
}

/* ════════════════════════════════════════════════════════════════
   Orders
   ════════════════════════════════════════════════════════════════ */

function OrdersTab({ orders, refreshing, onRecheck }) {
  const { t } = useTranslation();

  if (orders.length === 0) {
    return (
      <div className="sp-empty-state">
        <Inbox size={40} />
        <h3>{t("treasury_no_orders_title")}</h3>
        <p>{t("treasury_no_orders_hint")}</p>
      </div>
    );
  }

  return (
    <>
      <div className="sp-toolbar">
        <button className="sp-recheck-btn" onClick={onRecheck} disabled={refreshing}>
          <RefreshCw size={14} className={refreshing ? "spinning" : ""} />
          {t("recheck_payments")}
        </button>
      </div>
      <div className="sp-table-wrapper">
        <table className="sp-table">
          <thead>
            <tr>
              <th>{t("date")}</th>
              <th>{t("status")}</th>
              <th>{t("treasury_gateway")}</th>
              <th className="text-right">{t("treasury_fees_in_order_col")}</th>
              <th className="text-right">{t("amount")}</th>
              <th className="text-center"></th>
            </tr>
          </thead>
          <tbody>
            {orders.map((order) => (
              <tr key={order.id}>
                <td>
                  <span className="payment-date">
                    {new Date(order.createdAt).toLocaleString()}
                  </span>
                </td>
                <td>
                  <span className={`sp-status-badge ${orderBadgeClass(order.status)}`}>
                    {t(ORDER_STATUS_KEYS[order.status])}
                  </span>
                </td>
                <td>{t(GATEWAY_KEYS[order.gateway])}</td>
                <td className="text-right">{order.fees?.length ?? 0}</td>
                <td className="text-right">
                  <span className="sp-amount">
                    {fmtAmount(order.totalAmount)} {order.currency}
                  </span>
                </td>
                <td className="text-center">
                  {order.status === ORDER_STATUS.PendingPayment && order.redirectUrl && (
                    <a
                      className="sp-continue-link"
                      href={order.redirectUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      <ExternalLink size={12} /> {t("treasury_continue_payment")}
                    </a>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

/* ════════════════════════════════════════════════════════════════
   Payment history (fees inside settled orders)
   ════════════════════════════════════════════════════════════════ */

function HistoryTab({ rows, feeName }) {
  const { t } = useTranslation();

  if (rows.length === 0) {
    return (
      <div className="sp-empty-state">
        <DollarSign size={40} />
        <h3>{t("no_payment_history_title")}</h3>
        <p>{t("no_payment_history_message")}</p>
      </div>
    );
  }

  return (
    <div className="sp-table-wrapper">
      <table className="sp-table">
        <thead>
          <tr>
            <th>{t("date")}</th>
            <th>{t("fee")}</th>
            <th>{t("treasury_gateway")}</th>
            <th>{t("treasury_merchant_ref")}</th>
            <th className="text-right">{t("amount")}</th>
            <th className="text-center">{t("status")}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(({ order, fee }) => (
            <tr key={fee.id}>
              <td>
                <span className="payment-date">
                  {new Date(order.createdAt).toLocaleDateString()}
                </span>
              </td>
              <td>
                <span className="payment-fee-desc">{feeName(fee)}</span>
              </td>
              <td>{t(GATEWAY_KEYS[order.gateway])}</td>
              <td>
                {order.merchantOrderId
                  ? <span className="sp-mono">{order.merchantOrderId}</span>
                  : <span className="sp-dash">–</span>}
              </td>
              <td className="text-right">
                <span className="sp-amount green">
                  {fmtAmount(fee.totalAmount)} {fee.currency}
                </span>
              </td>
              <td className="text-center">
                <span className="sp-status-badge paid">{t("paid")}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default StudentPaymentsPage;
