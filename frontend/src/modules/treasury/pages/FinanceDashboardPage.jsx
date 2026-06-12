import { HandCoins, Hourglass, RotateCcw, ReceiptText, TrendingUp, PieChart } from "lucide-react";
import { useTranslation } from "react-i18next";
import PageHeader from "../../../core/components/PageHeader";
import { usePermission } from "../../../core/auth/usePermission";
import { StatCard, ChartCard, DonutChart, BarsChart, TrendChart } from "../../../core/components/dashboard/DashboardKit";
import { useTreasuryStats } from "../../../core/query/useDashboardStats";
import {
  FEE_STATUS,
  ORDER_STATUS,
  FEE_STATUS_KEYS,
  ORDER_STATUS_KEYS,
  fmtAmount,
} from "../../../core/services/treasuryService";

const FEE_COLORS = {
  [FEE_STATUS.Pending]: "#c9a84c",
  [FEE_STATUS.IncludedInOrder]: "#2563eb",
  [FEE_STATUS.Paid]: "#16a34a",
  [FEE_STATUS.Cancelled]: "#64748b",
  [FEE_STATUS.Refunded]: "#dc2626",
};

const ORDER_COLORS = {
  [ORDER_STATUS.Created]: "#64748b",
  [ORDER_STATUS.PendingPayment]: "#c9a84c",
  [ORDER_STATUS.Paid]: "#16a34a",
  [ORDER_STATUS.Failed]: "#dc2626",
  [ORDER_STATUS.Expired]: "#ea580c",
  [ORDER_STATUS.Refunded]: "#be185d",
  [ORDER_STATUS.Cancelled]: "#2e3591",
};

const fmtCount = (v) => Number(v ?? 0).toLocaleString("en-US");

/** Finance overview backed by GET /payments/fees/stats. Route is gated by the
 *  dashboard permission; the data needs payments.transactions.view. */
function FinanceDashboardPage() {
  const { t, i18n } = useTranslation();
  const { can } = usePermission();
  const canPayments = can("payments.transactions.view");

  const { data: stats, isLoading } = useTreasuryStats(canPayments);
  const currency = stats?.currency || "EGP";
  const money = (v) => `${fmtAmount(v)} ${currency}`;

  const slices = (rows, keys, colors) =>
    (rows || []).map((r) => ({
      name: t(keys[r.status] ?? String(r.status)),
      value: r.count,
      amount: r.amount,
      color: colors[r.status] || "#64748b",
    }));

  const feeSlices = slices(stats?.feesByStatus, FEE_STATUS_KEYS, FEE_COLORS);
  const orderSlices = slices(stats?.ordersByStatus, ORDER_STATUS_KEYS, ORDER_COLORS);

  // Zero-fill the last 12 months so gaps render as real dips, not equal spacing.
  const byMonth = new Map((stats?.monthlyCollected || []).map((p) => [`${p.year}-${p.month}`, p.amount]));
  const now = new Date();
  const monthly = Array.from({ length: 12 }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - 11 + i, 1);
    return {
      name: d.toLocaleDateString(i18n.language, { month: "short", year: "2-digit" }),
      value: byMonth.get(`${d.getFullYear()}-${d.getMonth() + 1}`) ?? 0,
    };
  });
  const hasMonthly = monthly.some((m) => m.value > 0);

  return (
    <div className="dk-page">
      <PageHeader
        icon={HandCoins}
        kicker={t("fin_dash_label")}
        title={t("fin_dash_title")}
        subtitle={t("fin_dash_subtitle")}
      />

      {!canPayments ? (
        <p style={{ marginTop: 18, color: "var(--color-text-secondary)" }}>
          {t("dash_no_permission_widgets")}
        </p>
      ) : (
        <>
          <div className="dk-stat-grid" style={{ marginTop: 18 }}>
            <StatCard
              icon={HandCoins}
              tone="green"
              label={t("fin_dash_collected")}
              value={money(stats?.totalCollected)}
              loading={isLoading}
              sub={stats ? t("fin_dash_fees_total", { count: fmtCount(stats.totalFees) }) : null}
            />
            <StatCard
              icon={Hourglass}
              tone="gold"
              label={t("fin_dash_outstanding")}
              value={money(stats?.outstandingAmount)}
              loading={isLoading}
            />
            <StatCard
              icon={RotateCcw}
              tone="red"
              label={t("fin_dash_refunded")}
              value={money(stats?.refundedAmount)}
              loading={isLoading}
            />
            <StatCard
              icon={ReceiptText}
              tone="navy"
              label={t("fin_dash_orders")}
              value={fmtCount(stats?.totalOrders)}
              loading={isLoading}
            />
          </div>

          <div className="dk-section">
            <ChartCard
              icon={TrendingUp}
              title={t("fin_dash_monthly")}
              subtitle={t("fin_dash_monthly_sub")}
              loading={isLoading}
              empty={!isLoading && !hasMonthly}
              emptyLabel={t("dash_no_data")}
            >
              <TrendChart data={monthly} valueFormatter={fmtAmount} />
            </ChartCard>

            <div className="dk-grid-2">
              <ChartCard
                icon={PieChart}
                title={t("fin_dash_fees_by_status")}
                loading={isLoading}
                empty={!isLoading && feeSlices.every((d) => !d.value)}
                emptyLabel={t("dash_no_data")}
              >
                <DonutChart data={feeSlices} centerLabel={t("fin_dash_fees_by_status")} />
              </ChartCard>
              <ChartCard
                icon={PieChart}
                title={t("fin_dash_orders_by_status")}
                loading={isLoading}
                empty={!isLoading && orderSlices.every((d) => !d.value)}
                emptyLabel={t("dash_no_data")}
              >
                <BarsChart data={orderSlices} height={300} />
              </ChartCard>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

export default FinanceDashboardPage;
