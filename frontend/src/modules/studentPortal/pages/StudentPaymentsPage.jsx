import { useState, useEffect, useCallback, useMemo } from "react";
import {
  Receipt, Tag, CheckCircle, AlertCircle, RefreshCw, Inbox,
  DollarSign, CreditCard,
} from "lucide-react";
import { useAuth } from "../../../core/auth/useAuth";
import * as invoiceService from "../../../core/services/invoiceService";
import * as paymentService from "../../../core/services/paymentService";
import "../styles/studentPayments.css";

/* ────────────────────────────────────────────────────────────────
   Helpers
   ──────────────────────────────────────────────────────────────── */

/** Derive a display year from an invoice's createdAt date.
 *  e.g. created Sept 2025 → "2025-2026", created Feb 2025 → "2024-2025" */
function deriveAcademicYear(createdAt) {
  const d = new Date(createdAt);
  const year = d.getUTCFullYear();
  const month = d.getUTCMonth(); // 0-indexed
  // Academic year starts in September (month 8)
  if (month >= 8) return `${year}-${year + 1}`;
  return `${year - 1}-${year}`;
}

/** Derive a term label from description/feeType hints */
function deriveTerm(description, feeType) {
  const text = `${description} ${feeType}`.toLowerCase();
  if (text.includes("الأول") || text.includes("fall")) return "FALL";
  if (text.includes("الثاني") || text.includes("spring")) return "SPRING";
  if (text.includes("summer") || text.includes("صيفي")) return "SUMMER";
  return "-";
}

function fmt(n) {
  return Number(n).toLocaleString("en-US", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

/* ════════════════════════════════════════════════════════════════
   Component
   ════════════════════════════════════════════════════════════════ */

const TABS = ["Unpaid Fees", "Installments", "Payment History", "All Fees"];

function StudentPaymentsPage() {
  const { user } = useAuth();

  const [invoices, setInvoices] = useState([]);
  const [detailedInvoices, setDetailedInvoices] = useState({});
  const [transactions, setTransactions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState(0);

  /* ── Data loading ─────────────────────────────────────────── */
  const loadData = useCallback(async (isRefresh = false) => {
    if (!user?.id) return;

    if (isRefresh) setRefreshing(true);
    else setLoading(true);

    setError(null);

    try {
      // 1. Fetch slim invoice list for student
      const invList = await invoiceService.fetchInvoicesForStudent(user.id);
      const invoiceArr = Array.isArray(invList) ? invList : [];
      setInvoices(invoiceArr);

      // 2. Fetch full details for each invoice (includes items)
      const detailPromises = invoiceArr.map((inv) =>
        invoiceService.fetchInvoice(inv.id).catch(() => null)
      );
      const details = await Promise.all(detailPromises);
      const detailMap = {};
      details.forEach((d) => {
        if (d) detailMap[d.id] = d;
      });
      setDetailedInvoices(detailMap);

      // 3. Fetch payment transactions for each invoice
      const txPromises = invoiceArr.map((inv) =>
        paymentService.fetchTransactionsForInvoice(inv.id).catch(() => [])
      );
      const txArrays = await Promise.all(txPromises);
      const allTxs = [];
      txArrays.forEach((arr, idx) => {
        const inv = invoiceArr[idx];
        (Array.isArray(arr) ? arr : []).forEach((tx) => {
          allTxs.push({ ...tx, _invoiceId: inv.id });
        });
      });
      // Sort by createdAt descending
      allTxs.sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
      setTransactions(allTxs);
    } catch (err) {
      setError(err.message || "Failed to load payment data");
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [user?.id]);

  useEffect(() => { loadData(); }, [loadData]);

  /* ── Derived data ─────────────────────────────────────────── */

  /** Build a "fee row" for each invoice, enriched with item-level info */
  const feeRows = useMemo(() => {
    return invoices.map((inv) => {
      const detail = detailedInvoices[inv.id];
      const items = detail?.items || [];
      const firstItem = items[0];

      const paidTxs = transactions.filter(
        (tx) => tx._invoiceId === inv.id && tx.status === paymentService.PAYMENT_TX_STATUS.Succeeded
      );
      const paidAmount = paidTxs.reduce((sum, tx) => sum + Number(tx.amount), 0);
      const remaining = Number(inv.totalAmount) - paidAmount;
      const discount = 0; // No discount field in the schema
      const netAmount = Number(inv.totalAmount) - discount;

      const description = firstItem?.description || "";
      const feeType = firstItem?.feeType || "";

      return {
        id: inv.id,
        title: description || feeType || `Invoice ${inv.id.slice(0, 8)}`,
        category: feeType,
        year: deriveAcademicYear(inv.createdAt),
        term: deriveTerm(description, feeType),
        amount: Number(inv.totalAmount),
        discount,
        netAmount,
        paid: paidAmount,
        remaining: Math.max(remaining, 0),
        dueDate: inv.dueAt,
        status: inv.status,
        createdAt: inv.createdAt,
      };
    });
  }, [invoices, detailedInvoices, transactions]);

  const unpaidFees = useMemo(
    () => feeRows.filter((r) => r.status === invoiceService.INVOICE_STATUS.Pending || r.status === invoiceService.INVOICE_STATUS.PartiallyPaid),
    [feeRows]
  );

  const paidTransactions = useMemo(() => {
    return transactions
      .filter((tx) => tx.status === paymentService.PAYMENT_TX_STATUS.Succeeded)
      .map((tx) => {
        const fee = feeRows.find((r) => r.id === tx._invoiceId);
        return { ...tx, _fee: fee };
      });
  }, [transactions, feeRows]);

  /** Group all fees by academic year */
  const feesByYear = useMemo(() => {
    const groups = {};
    feeRows.forEach((fee) => {
      if (!groups[fee.year]) groups[fee.year] = [];
      groups[fee.year].push(fee);
    });
    // Sort years descending
    return Object.entries(groups).sort((a, b) => b[0].localeCompare(a[0]));
  }, [feeRows]);

  /* ── Summary metrics ──────────────────────────────────────── */
  const totalFees = feeRows.reduce((s, r) => s + r.amount, 0);
  const totalDiscount = 0;
  const totalPaid = feeRows.reduce((s, r) => s + r.paid, 0);
  const balanceDue = feeRows.reduce((s, r) => s + r.remaining, 0);
  const feeCount = feeRows.length;

  /* ── Status helpers ───────────────────────────────────────── */
  function statusLabel(status) {
    if (status === invoiceService.INVOICE_STATUS.Paid) return "Paid";
    if (status === invoiceService.INVOICE_STATUS.PartiallyPaid) return "Partial";
    if (status === invoiceService.INVOICE_STATUS.Cancelled) return "Cancelled";
    return "Unpaid";
  }

  function statusClass(status) {
    if (status === invoiceService.INVOICE_STATUS.Paid) return "paid";
    if (status === invoiceService.INVOICE_STATUS.PartiallyPaid) return "partial";
    return "unpaid";
  }

  /* ── Render ───────────────────────────────────────────────── */

  if (loading) {
    return (
      <div className="student-payments">
        <div className="sp-loading">
          <div className="spinner"></div>
          <p>Loading your payment data…</p>
        </div>
      </div>
    );
  }

  if (error && invoices.length === 0) {
    return (
      <div className="student-payments">
        <div className="sp-header">
          <h1>Payments & Fees</h1>
          <p>View your financial obligations and payment history</p>
        </div>
        <div className="sp-error">
          <AlertCircle size={36} color="#dc2626" />
          <h3>Failed to load payment data</h3>
          <p>{error}</p>
          <button className="sp-retry-btn" onClick={() => loadData()}>
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="student-payments">
      {/* Header */}
      <div className="sp-header">
        <h1>Payments & Fees</h1>
        <p>View your financial obligations and payment history</p>
      </div>

      {/* Summary Cards */}
      <div className="sp-summary-grid">
        <div className="sp-summary-card total-fees">
          <div className="sp-card-icon total-fees">
            <Receipt size={22} />
          </div>
          <div className="sp-card-info">
            <div className="sp-card-label">Total Fees</div>
            <div className="sp-card-value">
              {fmt(totalFees)}
              <span className="currency">EGP</span>
            </div>
            <div className="sp-card-sub">{feeCount} fee(s)</div>
          </div>
        </div>

        <div className="sp-summary-card discount">
          <div className="sp-card-icon discount">
            <Tag size={22} />
          </div>
          <div className="sp-card-info">
            <div className="sp-card-label">Discount</div>
            <div className="sp-card-value">
              {fmt(totalDiscount)}
              <span className="currency">EGP</span>
            </div>
          </div>
        </div>

        <div className="sp-summary-card total-paid">
          <div className="sp-card-icon total-paid">
            <CheckCircle size={22} />
          </div>
          <div className="sp-card-info">
            <div className="sp-card-label">Total Paid</div>
            <div className="sp-card-value">
              {fmt(totalPaid)}
              <span className="currency">EGP</span>
            </div>
          </div>
        </div>

        <div className="sp-summary-card balance-due">
          <div className="sp-card-icon balance-due">
            <DollarSign size={22} />
          </div>
          <div className="sp-card-info">
            <div className="sp-card-label">Balance Due</div>
            <div className="sp-card-value">
              {fmt(balanceDue)}
              <span className="currency">EGP</span>
            </div>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div className="sp-tabs-bar">
        {TABS.map((label, idx) => {
          let badge = null;
          if (idx === 0 && unpaidFees.length > 0) {
            badge = (
              <span className="sp-tab-badge urgent">{unpaidFees.length}</span>
            );
          }
          if (idx === 2 && paidTransactions.length > 0) {
            badge = (
              <span className="sp-tab-badge">{paidTransactions.length}</span>
            );
          }
          return (
            <button
              key={label}
              className={`sp-tab${activeTab === idx ? " active" : ""}`}
              onClick={() => setActiveTab(idx)}
            >
              {label}
              {badge}
            </button>
          );
        })}
      </div>

      {/* Tab Content */}
      {activeTab === 0 && <UnpaidFeesTab fees={unpaidFees} refreshing={refreshing} onRecheck={() => loadData(true)} fmt={fmt} statusLabel={statusLabel} statusClass={statusClass} />}
      {activeTab === 1 && <InstallmentsTab />}
      {activeTab === 2 && <PaymentHistoryTab transactions={paidTransactions} fmt={fmt} />}
      {activeTab === 3 && <AllFeesTab feesByYear={feesByYear} fmt={fmt} statusLabel={statusLabel} statusClass={statusClass} />}
    </div>
  );
}

/* ════════════════════════════════════════════════════════════════
   Tab Components
   ════════════════════════════════════════════════════════════════ */

function UnpaidFeesTab({ fees, refreshing, onRecheck, fmt, statusLabel, statusClass }) {
  return (
    <>
      <div className="sp-toolbar">
        <button className="sp-recheck-btn" onClick={onRecheck} disabled={refreshing}>
          <RefreshCw size={14} className={refreshing ? "spinning" : ""} />
          Recheck Payments
        </button>
      </div>

      {fees.length === 0 ? (
        <div className="sp-empty-state">
          <CheckCircle size={40} />
          <h3>All fees are paid!</h3>
          <p>You have no outstanding payments at this time.</p>
        </div>
      ) : (
        <div className="sp-table-wrapper">
          <table className="sp-table">
            <thead>
              <tr>
                <th>Fee Title</th>
                <th>Category</th>
                <th>Year</th>
                <th>Term</th>
                <th className="text-right">Amount</th>
                <th className="text-right">Discount</th>
                <th className="text-right">Net Amount</th>
                <th className="text-right">Paid</th>
                <th className="text-right">Remaining</th>
                <th>Due Date</th>
                <th className="text-center">Status</th>
              </tr>
            </thead>
            <tbody>
              {fees.map((fee) => (
                <tr key={fee.id}>
                  <td><span className="sp-fee-title">{fee.title}</span></td>
                  <td>{fee.category || <span className="sp-dash">–</span>}</td>
                  <td>{fee.year}</td>
                  <td>{fee.term}</td>
                  <td className="text-right"><span className="sp-amount">{fmt(fee.amount)}</span></td>
                  <td className="text-right">{fee.discount > 0 ? fmt(fee.discount) : <span className="sp-dash">–</span>}</td>
                  <td className="text-right"><strong>{fmt(fee.netAmount)}</strong></td>
                  <td className="text-right">{fee.paid > 0 ? <span className="sp-amount paid">{fmt(fee.paid)}</span> : <span className="sp-dash">–</span>}</td>
                  <td className="text-right"><span className="sp-amount remaining">{fmt(fee.remaining)}</span></td>
                  <td>{fee.dueDate ? new Date(fee.dueDate).toLocaleDateString() : <span className="sp-dash">-</span>}</td>
                  <td className="text-center"><span className={`sp-status-badge ${statusClass(fee.status)}`}>{statusLabel(fee.status)}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}

function InstallmentsTab() {
  return (
    <div className="sp-empty-state">
      <CreditCard size={40} />
      <h3>No installment plans</h3>
      <p>You don't have any active installment plans at this time.</p>
    </div>
  );
}

function PaymentHistoryTab({ transactions, fmt }) {
  if (transactions.length === 0) {
    return (
      <div className="sp-empty-state">
        <Inbox size={40} />
        <h3>No payment history</h3>
        <p>No payment transactions have been recorded yet.</p>
      </div>
    );
  }

  return (
    <div className="sp-table-wrapper">
      <table className="sp-table">
        <thead>
          <tr>
            <th>Date</th>
            <th className="text-right">Amount</th>
            <th>Method</th>
            <th>Fee</th>
            <th>Year</th>
            <th>Term</th>
            <th>Order ID</th>
            <th>Notes</th>
            <th className="text-center">Status</th>
          </tr>
        </thead>
        <tbody>
          {transactions.map((tx) => (
            <tr key={tx.id}>
              <td>
                <span className="payment-date">
                  {new Date(tx.createdAt).toLocaleDateString("en-US", {
                    year: "numeric",
                    month: "short",
                    day: "numeric",
                  })},{" "}
                  {new Date(tx.createdAt).toLocaleTimeString("en-US", {
                    hour: "2-digit",
                    minute: "2-digit",
                    hour12: true,
                  })}
                </span>
              </td>
              <td className="text-right">
                <span className="sp-amount green">{fmt(tx.amount)} EGP</span>
              </td>
              <td>{tx.provider || "Online"}</td>
              <td>
                <span className="payment-fee-desc">
                  {tx._fee?.title || "-"}
                </span>
              </td>
              <td>{tx._fee?.year || "-"}</td>
              <td>{tx._fee?.term || "-"}</td>
              <td><span className="sp-dash">-</span></td>
              <td>
                <span className="payment-fee-desc">
                  {tx._fee?.title || "-"}
                </span>
              </td>
              <td className="text-center">
                <span className="sp-status-badge paid">Paid</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function AllFeesTab({ feesByYear, fmt, statusLabel, statusClass }) {
  if (feesByYear.length === 0) {
    return (
      <div className="sp-empty-state">
        <Inbox size={40} />
        <h3>No fees found</h3>
        <p>There are no fees associated with your account.</p>
      </div>
    );
  }

  return (
    <>
      {feesByYear.map(([year, fees]) => (
        <div key={year} className="sp-year-group">
          <div className="sp-year-header">
            <h3>Academic Year {year}</h3>
            <span className="sp-year-count">{fees.length} fee(s)</span>
          </div>

          <div className="sp-table-wrapper">
            <table className="sp-table">
              <thead>
                <tr>
                  <th>Fee Title</th>
                  <th>Category</th>
                  <th>Term</th>
                  <th className="text-right">Amount</th>
                  <th className="text-right">Discount</th>
                  <th className="text-right">Net</th>
                  <th className="text-right">Paid</th>
                  <th>Due Date</th>
                  <th className="text-center">Status</th>
                </tr>
              </thead>
              <tbody>
                {fees.map((fee) => (
                  <tr key={fee.id}>
                    <td><span className="sp-fee-title">{fee.title}</span></td>
                    <td>{fee.category || <span className="sp-dash">–</span>}</td>
                    <td>{fee.term}</td>
                    <td className="text-right"><span className="sp-amount">{fmt(fee.amount)}</span></td>
                    <td className="text-right">{fee.discount > 0 ? fmt(fee.discount) : <span className="sp-dash">–</span>}</td>
                    <td className="text-right"><strong>{fmt(fee.netAmount)}</strong></td>
                    <td className="text-right">
                      {fee.paid > 0 ? (
                        <span className="sp-amount paid">{fmt(fee.paid)}</span>
                      ) : (
                        <span className="sp-dash">–</span>
                      )}
                    </td>
                    <td>{fee.dueDate ? new Date(fee.dueDate).toLocaleDateString() : <span className="sp-dash">-</span>}</td>
                    <td className="text-center">
                      <span className={`sp-status-badge ${statusClass(fee.status)}`}>
                        {statusLabel(fee.status)}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ))}
    </>
  );
}

export default StudentPaymentsPage;
