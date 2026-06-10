import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  HandCoins, RefreshCw, CheckCircle2, UserRound, ArrowLeftRight,
  CreditCard, Landmark, Globe, ShoppingCart, X, Inbox, Copy, Check,
  ExternalLink, Eye, AlertCircle, Info,
} from "lucide-react";
import { useToast } from "../../../core/components/Toast";
import { useCanDo } from "../../../core/auth/usePermission";
import StatusBadge from "../../../core/components/StatusBadge";
import EmptyState from "../../../core/components/EmptyState";
import { SkeletonTable } from "../../../core/components/Skeleton";
import StudentPicker from "../components/StudentPicker";
import OrderDetailDrawer from "../components/OrderDetailDrawer";
import * as studentService from "../../../core/services/studentService";
import {
  useReceipts, buildReceiptIndex, useUnpaidFees, useStudentOrders,
  useCreateOrder,
} from "../../../core/query/useTreasury";
import {
  GATEWAY, GATEWAY_KEYS, ORDER_STATUS, ORDER_STATUS_KEYS, ORDER_STATUS_BADGE,
  fmtAmount, apiErrorMessage,
} from "../../../core/services/treasuryService";
import "../styles/treasury.css";

const GATEWAYS = [
  { value: GATEWAY.Mastercard, labelKey: GATEWAY_KEYS[GATEWAY.Mastercard], descKey: "treasury_gateway_mastercard_desc", Icon: CreditCard },
  { value: GATEWAY.BankMisr, labelKey: GATEWAY_KEYS[GATEWAY.BankMisr], descKey: "treasury_gateway_bankmisr_desc", Icon: Landmark },
  { value: GATEWAY.EFinance, labelKey: GATEWAY_KEYS[GATEWAY.EFinance], descKey: "treasury_gateway_efinance_desc", Icon: Globe },
];

const ORDER_FILTERS = [
  { id: "all", labelKey: "treasury_filter_all", match: () => true },
  {
    id: "open",
    labelKey: "treasury_filter_open",
    match: (o) => o.status === ORDER_STATUS.Created || o.status === ORDER_STATUS.PendingPayment,
  },
  { id: "paid", labelKey: "treasury_filter_paid", match: (o) => o.status === ORDER_STATUS.Paid },
  {
    id: "closed",
    labelKey: "treasury_filter_closed",
    match: (o) =>
      o.status === ORDER_STATUS.Failed ||
      o.status === ORDER_STATUS.Expired ||
      o.status === ORDER_STATUS.Cancelled ||
      o.status === ORDER_STATUS.Refunded,
  },
];

/** Sum amounts per currency: [{ currency, total }] sorted by total desc. */
function sumByCurrency(rows, amountOf) {
  const map = {};
  for (const row of rows) {
    const c = row.currency || "EGP";
    map[c] = (map[c] || 0) + Number(amountOf(row));
  }
  return Object.entries(map)
    .map(([currency, total]) => ({ currency, total }))
    .sort((a, b) => b.total - a.total);
}

function MoneyList({ sums, emptyDash = true }) {
  if (!sums.length) return emptyDash ? <span>—</span> : null;
  return (
    <span className="ty-money-list">
      {sums.map((s, i) => (
        <span key={s.currency}>
          {i > 0 && " · "}
          {fmtAmount(s.total)} <span className="ty-currency">{s.currency}</span>
        </span>
      ))}
    </span>
  );
}

export default function TreasuryHubPage() {
  const { t } = useTranslation();
  const { addToast } = useToast();
  const canTransact = useCanDo("payments.transactions", 2);

  const [searchParams, setSearchParams] = useSearchParams();
  const urlStudentId = searchParams.get("studentId");

  const [pickedStudent, setPickedStudent] = useState(null);
  const [selectedIds, setSelectedIds] = useState(new Set());
  const [gateway, setGateway] = useState(null);
  const [builderError, setBuilderError] = useState("");
  const [activeOrderId, setActiveOrderId] = useState(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [orderFilter, setOrderFilter] = useState("all");

  /* Deep link: /admin/finance/treasury?studentId=… — the active student is
     derived (explicit pick wins over the URL) so no state syncing is needed. */
  const { data: urlStudent } = useQuery({
    queryKey: ["students", "byId", urlStudentId],
    queryFn: () => studentService.fetchStudentById(urlStudentId),
    enabled: !!urlStudentId && !pickedStudent,
  });
  const student = pickedStudent ?? (urlStudentId ? urlStudent ?? null : null);

  const selectStudent = (s) => {
    setPickedStudent(s);
    setSelectedIds(new Set());
    setBuilderError("");
    setOrderFilter("all");
    setSearchParams({ studentId: s.id }, { replace: true });
  };

  const clearStudent = () => {
    setPickedStudent(null);
    setSelectedIds(new Set());
    setBuilderError("");
    setSearchParams({}, { replace: true });
  };

  /* Data */
  const { data: receipts = [] } = useReceipts();
  const receiptIndex = useMemo(() => buildReceiptIndex(receipts), [receipts]);

  const {
    data: fees = [],
    isLoading: feesLoading,
    error: feesError,
    refetch: refetchFees,
    isFetching: feesFetching,
  } = useUnpaidFees(student?.id);

  const {
    data: orders = [],
    isLoading: ordersLoading,
    error: ordersError,
    refetch: refetchOrders,
  } = useStudentOrders(student?.id);

  const createOrder = useCreateOrder();

  /* Derived */
  const selectedFees = useMemo(
    () => fees.filter((f) => selectedIds.has(f.id)),
    [fees, selectedIds]
  );
  const activeCurrency = selectedFees[0]?.currency ?? null;
  const currencies = useMemo(
    () => [...new Set(fees.map((f) => f.currency || "EGP"))],
    [fees]
  );
  const multiCurrency = currencies.length > 1;
  const selectedTotal = selectedFees.reduce((s, f) => s + Number(f.totalAmount), 0);

  const outstandingSums = useMemo(
    () => sumByCurrency(fees, (f) => f.totalAmount),
    [fees]
  );
  const paidOrders = orders.filter((o) => o.status === ORDER_STATUS.Paid);
  const collectedSums = useMemo(
    () => sumByCurrency(paidOrders, (o) => o.totalAmount),
    [paidOrders]
  );
  const inFlightCount = orders.filter(
    (o) => o.status === ORDER_STATUS.PendingPayment || o.status === ORDER_STATUS.Created
  ).length;

  const visibleOrders = useMemo(() => {
    const filter = ORDER_FILTERS.find((f) => f.id === orderFilter) || ORDER_FILTERS[0];
    return orders.filter(filter.match);
  }, [orders, orderFilter]);

  /* Actions */
  const toggleFee = (fee) => {
    if (activeCurrency && fee.currency !== activeCurrency && !selectedIds.has(fee.id)) return;
    setBuilderError("");
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(fee.id)) next.delete(fee.id);
      else next.add(fee.id);
      return next;
    });
  };

  const selectAllInCurrency = (currency) => {
    setBuilderError("");
    setSelectedIds(new Set(fees.filter((f) => f.currency === currency).map((f) => f.id)));
  };

  const handleCreateOrder = async () => {
    if (!selectedIds.size || gateway === null) return;
    setBuilderError("");
    try {
      const order = await createOrder.mutateAsync({
        studentId: student.id,
        feeIds: [...selectedIds],
        gateway,
      });
      setSelectedIds(new Set());
      addToast(t("treasury_order_created_toast"), "success");
      setActiveOrderId(order.id);
      setDrawerOpen(true);
    } catch (err) {
      setBuilderError(apiErrorMessage(err, t("treasury_create_order_failed")));
    }
  };

  const openOrder = (orderId) => {
    setActiveOrderId(orderId);
    setDrawerOpen(true);
  };

  /* ── Render ── */
  return (
    <div className="ty-page">
      <div className="ty-header">
        <div className="ty-header-left">
          <span className="ty-header-icon" aria-hidden="true">
            <HandCoins size={22} />
          </span>
          <div>
            <h1>{t("treasury_hub_title")}</h1>
            <p>{t("treasury_hub_subtitle")}</p>
          </div>
        </div>
        {student && (
          <button className="ty-btn" onClick={clearStudent}>
            <ArrowLeftRight size={14} aria-hidden="true" />
            {t("treasury_change_student")}
          </button>
        )}
      </div>

      {!student ? (
        <div className="ty-hero">
          <div className="ty-hero-card">
            <h2>{t("treasury_pick_student_title")}</h2>
            <p>{t("treasury_pick_student_hint")}</p>
            <StudentPicker onSelect={selectStudent} />
          </div>
        </div>
      ) : (
        <>
          {/* Student banner */}
          <div className="ty-student-banner">
            <span className="ty-student-avatar" aria-hidden="true">
              <UserRound size={20} />
            </span>
            <div className="ty-student-id">
              <div className="ty-student-name">{student.name}</div>
              <div className="ty-student-sub">
                {student.studentCode && <span className="ty-code-pill">{student.studentCode}</span>}
                {student.email && <span>{student.email}</span>}
              </div>
            </div>
            <div className="ty-banner-stats">
              <div className="ty-banner-stat">
                <div className="ty-banner-stat-value ty-warn">
                  <MoneyList sums={outstandingSums} />
                </div>
                <div className="ty-banner-stat-label">{t("treasury_outstanding")}</div>
              </div>
              <div className="ty-banner-stat">
                <div className="ty-banner-stat-value">{inFlightCount}</div>
                <div className="ty-banner-stat-label">{t("treasury_open_orders")}</div>
              </div>
              <div className="ty-banner-stat">
                <div className="ty-banner-stat-value ty-success">
                  <MoneyList sums={collectedSums} />
                </div>
                <div className="ty-banner-stat-label">{t("treasury_collected")}</div>
              </div>
            </div>
          </div>

          <div className="ty-main-grid">
            {/* Outstanding fees */}
            <section className="ty-card ty-fees-card" aria-label={t("treasury_outstanding_fees")}>
              <div className="ty-card-head">
                <h2>
                  {t("treasury_outstanding_fees")}
                  <span className="ty-count-chip">{fees.length}</span>
                </h2>
                <button
                  className="ty-icon-btn"
                  onClick={() => refetchFees()}
                  aria-label={t("refresh")}
                  title={t("refresh")}
                >
                  <RefreshCw size={14} className={feesFetching ? "ty-spin" : ""} />
                </button>
              </div>

              {multiCurrency && (
                <div className="ty-alert ty-alert-info ty-alert-slim">
                  <Info size={14} aria-hidden="true" />
                  <span>{t("treasury_multi_currency_hint")}</span>
                </div>
              )}

              {feesLoading && <SkeletonTable rows={4} cols={3} />}

              {feesError && (
                <div className="ty-alert ty-alert-error" role="alert">
                  <AlertCircle size={15} aria-hidden="true" />
                  <span>{apiErrorMessage(feesError, t("treasury_fees_load_failed"))}</span>
                  <button className="ty-btn ty-btn-slim" onClick={() => refetchFees()}>
                    {t("retry")}
                  </button>
                </div>
              )}

              {!feesLoading && !feesError && fees.length === 0 && (
                <EmptyState
                  icon={CheckCircle2}
                  title={t("treasury_all_settled_title")}
                  message={t("treasury_all_settled_hint")}
                />
              )}

              {!feesLoading && !feesError &&
                currencies.map((currency) => {
                  const group = fees.filter((f) => (f.currency || "EGP") === currency);
                  if (!group.length) return null;
                  const groupDisabled = activeCurrency && activeCurrency !== currency;
                  return (
                    <div key={currency} className="ty-fee-group">
                      {(multiCurrency || fees.length > 1) && (
                        <div className="ty-fee-group-head">
                          {multiCurrency && (
                            <span className="ty-currency-tag">{currency}</span>
                          )}
                          {canTransact && (
                            <button
                              className="ty-link-btn"
                              onClick={() => selectAllInCurrency(currency)}
                              disabled={!!groupDisabled && selectedIds.size > 0}
                            >
                              {t("treasury_select_all")}
                            </button>
                          )}
                        </div>
                      )}
                      {group.map((fee) => {
                        const receipt = receiptIndex[fee.receiptId];
                        const checked = selectedIds.has(fee.id);
                        const disabled = !canTransact || (groupDisabled && !checked);
                        return (
                          <label
                            key={fee.id}
                            className={`ty-fee-row${checked ? " selected" : ""}${disabled ? " disabled" : ""}`}
                            title={
                              groupDisabled && !checked
                                ? t("treasury_currency_locked_hint", { currency: activeCurrency })
                                : undefined
                            }
                          >
                            {canTransact && (
                              <input
                                type="checkbox"
                                checked={checked}
                                disabled={disabled}
                                onChange={() => toggleFee(fee)}
                                aria-label={receipt?.name || t("treasury_fee_fallback_name")}
                              />
                            )}
                            <span className="ty-fee-main">
                              <span className="ty-fee-name">
                                {receipt?.name || t("treasury_fee_fallback_name")}
                              </span>
                              <span className="ty-fee-sub">
                                {fee.quantity} × {fmtAmount(fee.unitAmount)}
                                {fee.sourceModule && (
                                  <span className="ty-meta-chip">{fee.sourceModule}</span>
                                )}
                                <span className="ty-fee-date">
                                  {new Date(fee.createdAt).toLocaleDateString()}
                                </span>
                              </span>
                            </span>
                            <span className="ty-fee-amount">
                              {fmtAmount(fee.totalAmount)}
                              <span className="ty-currency">{fee.currency}</span>
                            </span>
                          </label>
                        );
                      })}
                    </div>
                  );
                })}
            </section>

            {/* Order builder */}
            {canTransact && (
              <aside className="ty-card ty-builder" aria-label={t("treasury_new_order")}>
                <div className="ty-card-head">
                  <h2>
                    <ShoppingCart size={15} aria-hidden="true" /> {t("treasury_new_order")}
                  </h2>
                </div>

                {selectedFees.length === 0 ? (
                  <p className="ty-builder-empty">{t("treasury_builder_empty_hint")}</p>
                ) : (
                  <>
                    <ul className="ty-builder-list">
                      {selectedFees.map((fee) => (
                        <li key={fee.id}>
                          <span className="ty-builder-fee-name">
                            {receiptIndex[fee.receiptId]?.name || t("treasury_fee_fallback_name")}
                          </span>
                          <span className="ty-builder-fee-amount">
                            {fmtAmount(fee.totalAmount)}
                          </span>
                          <button
                            className="ty-icon-btn"
                            onClick={() => toggleFee(fee)}
                            aria-label={t("remove")}
                          >
                            <X size={12} />
                          </button>
                        </li>
                      ))}
                    </ul>
                    <div className="ty-builder-total">
                      <span>{t("treasury_order_total")}</span>
                      <span className="ty-builder-total-value">
                        {fmtAmount(selectedTotal)}
                        <span className="ty-currency">{activeCurrency}</span>
                      </span>
                    </div>
                  </>
                )}

                <div className="ty-gateway-label">{t("treasury_choose_gateway")}</div>
                <div className="ty-gateways" role="radiogroup" aria-label={t("treasury_choose_gateway")}>
                  {GATEWAYS.map(({ value, labelKey, descKey, Icon }) => (
                    <button
                      key={value}
                      type="button"
                      role="radio"
                      aria-checked={gateway === value}
                      className={`ty-gateway-card${gateway === value ? " selected" : ""}`}
                      onClick={() => setGateway(value)}
                    >
                      <Icon size={16} aria-hidden="true" />
                      <span className="ty-gateway-name">{t(labelKey)}</span>
                      <span className="ty-gateway-desc">{t(descKey)}</span>
                    </button>
                  ))}
                </div>

                {builderError && (
                  <div className="ty-alert ty-alert-error" role="alert">
                    <AlertCircle size={14} aria-hidden="true" />
                    <span>{builderError}</span>
                  </div>
                )}

                <button
                  className="ty-btn ty-btn-primary ty-btn-block"
                  disabled={!selectedIds.size || gateway === null || createOrder.isPending}
                  onClick={handleCreateOrder}
                >
                  {createOrder.isPending
                    ? t("treasury_creating_order")
                    : t("treasury_create_order")}
                </button>
                {(!selectedIds.size || gateway === null) && (
                  <p className="ty-builder-hint">
                    {!selectedIds.size
                      ? t("treasury_create_needs_fees")
                      : t("treasury_create_needs_gateway")}
                  </p>
                )}
              </aside>
            )}
          </div>

          {/* Orders */}
          <section className="ty-card ty-orders-card" aria-label={t("treasury_orders")}>
            <div className="ty-card-head">
              <h2>
                {t("treasury_orders")}
                <span className="ty-count-chip">{orders.length}</span>
              </h2>
              <div className="ty-order-filters" role="tablist">
                {ORDER_FILTERS.map((f) => (
                  <button
                    key={f.id}
                    role="tab"
                    aria-selected={orderFilter === f.id}
                    className={`ty-chip-btn${orderFilter === f.id ? " active" : ""}`}
                    onClick={() => setOrderFilter(f.id)}
                  >
                    {t(f.labelKey)}
                  </button>
                ))}
              </div>
            </div>

            {ordersLoading && <SkeletonTable rows={3} cols={5} />}
            {ordersError && (
              <div className="ty-alert ty-alert-error" role="alert">
                <AlertCircle size={15} aria-hidden="true" />
                <span>{apiErrorMessage(ordersError, t("treasury_orders_load_failed"))}</span>
                <button className="ty-btn ty-btn-slim" onClick={() => refetchOrders()}>
                  {t("retry")}
                </button>
              </div>
            )}
            {!ordersLoading && !ordersError && visibleOrders.length === 0 && (
              <EmptyState
                icon={Inbox}
                title={t("treasury_no_orders_title")}
                message={
                  orderFilter === "all"
                    ? t("treasury_no_orders_hint")
                    : t("treasury_no_orders_filter_hint")
                }
              />
            )}
            {!ordersLoading && !ordersError &&
              visibleOrders.map((order) => (
                <OrderRow
                  key={order.id}
                  order={order}
                  onOpen={() => openOrder(order.id)}
                  t={t}
                  addToast={addToast}
                />
              ))}
          </section>
        </>
      )}

      <OrderDetailDrawer
        orderId={activeOrderId}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        receiptIndex={receiptIndex}
        canTransact={canTransact}
      />
    </div>
  );
}

function OrderRow({ order, onOpen, t, addToast }) {
  const [copied, setCopied] = useState(false);
  const isPending = order.status === ORDER_STATUS.PendingPayment;

  const copyLink = async (e) => {
    e.stopPropagation();
    try {
      await navigator.clipboard.writeText(order.redirectUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 1800);
    } catch {
      addToast(t("treasury_copy_failed"), "error");
    }
  };

  return (
    <div
      className="ty-order-row"
      onClick={onOpen}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          onOpen();
        }
      }}
    >
      <div className="ty-order-row-main">
        <div className="ty-order-row-top">
          <StatusBadge
            status={ORDER_STATUS_BADGE[order.status]}
            label={t(ORDER_STATUS_KEYS[order.status])}
          />
          <span className="ty-meta-chip">{t(GATEWAY_KEYS[order.gateway])}</span>
          {order.merchantOrderId && (
            <span className="ty-code-pill" title={t("treasury_merchant_ref")}>
              {order.merchantOrderId}
            </span>
          )}
        </div>
        <div className="ty-order-row-sub">
          {t("treasury_fees_count", { count: order.fees?.length ?? 0 })}
          {" · "}
          {new Date(order.createdAt).toLocaleString()}
        </div>
      </div>
      <div className="ty-order-row-amount">
        {fmtAmount(order.totalAmount)}
        <span className="ty-currency">{order.currency}</span>
      </div>
      <div className="ty-order-row-actions" onClick={(e) => e.stopPropagation()}>
        {isPending && order.redirectUrl && (
          <>
            <a
              className="ty-icon-btn"
              href={order.redirectUrl}
              target="_blank"
              rel="noopener noreferrer"
              aria-label={t("treasury_open_payment_page")}
              title={t("treasury_open_payment_page")}
            >
              <ExternalLink size={14} />
            </a>
            <button
              className="ty-icon-btn"
              onClick={copyLink}
              aria-label={t("treasury_copy_payment_link")}
              title={t("treasury_copy_payment_link")}
            >
              {copied ? <Check size={14} /> : <Copy size={14} />}
            </button>
          </>
        )}
        <button
          className="ty-icon-btn"
          onClick={onOpen}
          aria-label={t("treasury_order_details")}
          title={t("treasury_order_details")}
        >
          <Eye size={14} />
        </button>
      </div>
    </div>
  );
}
