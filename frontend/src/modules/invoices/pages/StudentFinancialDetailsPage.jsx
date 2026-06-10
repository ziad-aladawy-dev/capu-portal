import { useState, useEffect, useCallback, useMemo } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  Receipt, Tag, CheckCircle, AlertCircle, RefreshCw, Inbox,
  DollarSign, ArrowLeft, Plus,
} from "lucide-react";
import * as invoiceService from "../../../core/services/invoiceService";
import * as paymentService from "../../../core/services/paymentService";
import * as studentService from "../../../core/services/studentService";
import "../styles/adminFinance.css";

/* ────────────────────────────────────────────────────────────────
   Helpers
   ──────────────────────────────────────────────────────────────── */
function deriveAcademicYear(createdAt) {
  const d = new Date(createdAt);
  const year = d.getUTCFullYear();
  const month = d.getUTCMonth(); 
  if (month >= 8) return `${year}-${year + 1}`;
  return `${year - 1}-${year}`;
}

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

const TABS = ["Unpaid Fees", "Payment History", "All Fees"];

function StudentFinancialDetailsPage() {
  const { studentId } = useParams();
  const navigate = useNavigate();

  const [student, setStudent] = useState(null);
  const [invoices, setInvoices] = useState([]);
  const [detailedInvoices, setDetailedInvoices] = useState({});
  const [transactions, setTransactions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState(0);

  const loadData = useCallback(async (isRefresh = false) => {
    if (!studentId) return;

    if (isRefresh) setRefreshing(true);
    else setLoading(true);

    setError(null);

    try {
      const [stProfile, invList] = await Promise.all([
        studentService.fetchStudentById(studentId).catch(() => null),
        invoiceService.fetchInvoicesForStudent(studentId)
      ]);

      setStudent(stProfile);
      
      const invoiceArr = Array.isArray(invList) ? invList : [];
      setInvoices(invoiceArr);

      // Fetch full details
      const detailPromises = invoiceArr.map((inv) =>
        invoiceService.fetchInvoice(inv.id).catch(() => null)
      );
      const details = await Promise.all(detailPromises);
      const detailMap = {};
      details.forEach((d) => {
        if (d) detailMap[d.id] = d;
      });
      setDetailedInvoices(detailMap);

      // Fetch txs
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
      allTxs.sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));
      setTransactions(allTxs);
    } catch (err) {
      setError(err.message || "Failed to load financial data");
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [studentId]);

  useEffect(() => { loadData(); }, [loadData]);

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
      const discount = 0; 
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

  const feesByYear = useMemo(() => {
    const groups = {};
    feeRows.forEach((fee) => {
      if (!groups[fee.year]) groups[fee.year] = [];
      groups[fee.year].push(fee);
    });
    return Object.entries(groups).sort((a, b) => b[0].localeCompare(a[0]));
  }, [feeRows]);

  const totalFees = feeRows.reduce((s, r) => s + r.amount, 0);
  const totalDiscount = 0;
  const totalPaid = feeRows.reduce((s, r) => s + r.paid, 0);
  const balanceDue = feeRows.reduce((s, r) => s + r.remaining, 0);

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

  if (loading) {
    return (
      <div className="admin-finance">
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '60vh' }}>
          <div style={{ textAlign: 'center', color: '#64748b' }}>
            <div className="spinner" style={{ marginBottom: 16 }}></div>
            <p>Loading student financial profile…</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="admin-finance">
      <div style={{ marginBottom: 16 }}>
        <button className="af-btn af-btn-outline" onClick={() => navigate('/admin/finance')} style={{ padding: '6px 12px' }}>
          <ArrowLeft size={14} /> Back to Directory
        </button>
      </div>

      <div className="af-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
          <div className="af-student-avatar" style={{ width: 56, height: 56, fontSize: 20 }}>
            {student?.name ? student.name.charAt(0).toUpperCase() : "?"}
          </div>
          <div>
            <h1>{student?.name || "Student Profile"}</h1>
            <p>{student?.studentCode || studentId} · {student?.nationalId || "No NID"}</p>
          </div>
        </div>
        <div style={{ display: 'flex', gap: 12 }}>
          <button className="af-btn af-btn-outline" onClick={() => loadData(true)} disabled={refreshing}>
            <RefreshCw size={14} className={refreshing ? "spinning" : ""} />
            Sync Vault
          </button>
          <button className="af-btn af-btn-primary" onClick={() => navigate(`/admin/invoices`)}>
            <Plus size={14} /> Issue Invoice
          </button>
        </div>
      </div>

      {error && (
        <div style={{ background: '#fef2f2', color: '#dc2626', padding: 16, borderRadius: 12, marginBottom: 24, display: 'flex', gap: 12 }}>
          <AlertCircle size={20} />
          <span>{error}</span>
        </div>
      )}

      {/* Summary Cards */}
      <div className="af-summary-grid">
        <div className="af-summary-card revenue">
          <div className="af-card-icon"><Receipt size={24} /></div>
          <div className="af-card-info">
            <div className="af-card-label">Total Billed</div>
            <div className="af-card-value">{fmt(totalFees)} <span className="currency">EGP</span></div>
          </div>
        </div>
        <div className="af-summary-card collected">
          <div className="af-card-icon"><CheckCircle size={24} /></div>
          <div className="af-card-info">
            <div className="af-card-label">Total Paid</div>
            <div className="af-card-value">{fmt(totalPaid)} <span className="currency">EGP</span></div>
          </div>
        </div>
        <div className="af-summary-card pending">
          <div className="af-card-icon"><DollarSign size={24} /></div>
          <div className="af-card-info">
            <div className="af-card-label">Balance Due</div>
            <div className="af-card-value" style={{ color: balanceDue > 0 ? '#ef4444' : '#0f1235' }}>
              {fmt(balanceDue)} <span className="currency">EGP</span>
            </div>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', gap: 8, marginBottom: 20, borderBottom: '1px solid #e2e8f0', paddingBottom: 16 }}>
        {TABS.map((label, idx) => {
          let badge = null;
          if (idx === 0 && unpaidFees.length > 0) badge = <span style={{ marginLeft: 6, background: '#ef4444', color: 'white', padding: '2px 6px', borderRadius: 10, fontSize: 10, fontWeight: 700 }}>{unpaidFees.length}</span>;
          if (idx === 1 && paidTransactions.length > 0) badge = <span style={{ marginLeft: 6, background: '#e2e8f0', color: '#334155', padding: '2px 6px', borderRadius: 10, fontSize: 10, fontWeight: 700 }}>{paidTransactions.length}</span>;
          
          return (
            <button
              key={label}
              onClick={() => setActiveTab(idx)}
              style={{
                background: activeTab === idx ? '#1a1f5e' : 'transparent',
                color: activeTab === idx ? 'white' : '#64748b',
                border: 'none',
                padding: '8px 16px',
                borderRadius: 20,
                fontSize: 13,
                fontWeight: 600,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                transition: 'all 0.2s',
              }}
            >
              {label} {badge}
            </button>
          );
        })}
      </div>

      {/* Tab Content */}
      {activeTab === 0 && <UnpaidFeesTab fees={unpaidFees} fmt={fmt} statusLabel={statusLabel} statusClass={statusClass} navigate={navigate} />}
      {activeTab === 1 && <PaymentHistoryTab transactions={paidTransactions} fmt={fmt} />}
      {activeTab === 2 && <AllFeesTab feesByYear={feesByYear} fmt={fmt} statusLabel={statusLabel} statusClass={statusClass} navigate={navigate} />}
    </div>
  );
}

function UnpaidFeesTab({ fees, fmt, statusLabel, statusClass, navigate }) {
  if (fees.length === 0) {
    return (
      <div className="af-empty-state">
        <CheckCircle size={40} />
        <h3>All fees are settled</h3>
        <p>This student has no outstanding balances.</p>
      </div>
    );
  }

  return (
    <div className="af-table-wrapper">
      <table className="af-table">
        <thead>
          <tr>
            <th>Fee Title</th>
            <th>Year</th>
            <th>Term</th>
            <th className="text-right">Amount</th>
            <th className="text-right">Paid</th>
            <th className="text-right">Remaining</th>
            <th className="text-center">Status</th>
          </tr>
        </thead>
        <tbody>
          {fees.map((fee) => (
            <tr key={fee.id} onClick={() => navigate(`/admin/invoices/${fee.id}`)}>
              <td><strong>{fee.title}</strong></td>
              <td>{fee.year}</td>
              <td>{fee.term}</td>
              <td className="text-right"><span className="af-amount neutral">{fmt(fee.amount)}</span></td>
              <td className="text-right">{fee.paid > 0 ? <span className="af-amount positive">{fmt(fee.paid)}</span> : <span>–</span>}</td>
              <td className="text-right"><span className="af-amount negative">{fmt(fee.remaining)}</span></td>
              <td className="text-center"><span className={`af-badge ${statusClass(fee.status)}`}>{statusLabel(fee.status)}</span></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function PaymentHistoryTab({ transactions, fmt }) {
  if (transactions.length === 0) {
    return (
      <div className="af-empty-state">
        <Inbox size={40} />
        <h3>No payment history</h3>
        <p>No payment transactions have been recorded yet.</p>
      </div>
    );
  }

  return (
    <div className="af-table-wrapper">
      <table className="af-table">
        <thead>
          <tr>
            <th>Date</th>
            <th className="text-right">Amount</th>
            <th>Method</th>
            <th>Fee Reference</th>
            <th>Notes</th>
            <th className="text-center">Status</th>
          </tr>
        </thead>
        <tbody>
          {transactions.map((tx) => (
            <tr key={tx.id}>
              <td>
                <span style={{ fontSize: 13, fontWeight: 500 }}>
                  {new Date(tx.createdAt).toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" })}
                </span>
                <span style={{ fontSize: 11, color: '#64748b', display: 'block' }}>
                  {new Date(tx.createdAt).toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit" })}
                </span>
              </td>
              <td className="text-right">
                <span className="af-amount positive">{fmt(tx.amount)} EGP</span>
              </td>
              <td>{tx.provider || "System"}</td>
              <td>{tx._fee?.title || "-"}</td>
              <td style={{ fontSize: 12, color: '#64748b' }}>{tx.notes || "-"}</td>
              <td className="text-center"><span className="af-badge paid">Success</span></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function AllFeesTab({ feesByYear, fmt, statusLabel, statusClass, navigate }) {
  if (feesByYear.length === 0) {
    return (
      <div className="af-empty-state">
        <Inbox size={40} />
        <h3>No fees found</h3>
        <p>There are no invoices associated with this student.</p>
      </div>
    );
  }

  return (
    <>
      {feesByYear.map(([year, fees]) => (
        <div key={year} style={{ marginBottom: 24 }}>
          <h3 style={{ fontSize: 15, fontWeight: 600, color: '#1a1f5e', marginBottom: 12 }}>Academic Year {year}</h3>
          <div className="af-table-wrapper">
            <table className="af-table">
              <thead>
                <tr>
                  <th>Fee Title</th>
                  <th>Term</th>
                  <th className="text-right">Amount</th>
                  <th className="text-right">Paid</th>
                  <th className="text-center">Status</th>
                </tr>
              </thead>
              <tbody>
                {fees.map((fee) => (
                  <tr key={fee.id} onClick={() => navigate(`/admin/invoices/${fee.id}`)}>
                    <td><strong>{fee.title}</strong></td>
                    <td>{fee.term}</td>
                    <td className="text-right"><span className="af-amount neutral">{fmt(fee.amount)}</span></td>
                    <td className="text-right">{fee.paid > 0 ? <span className="af-amount positive">{fmt(fee.paid)}</span> : <span>–</span>}</td>
                    <td className="text-center">
                      <span className={`af-badge ${statusClass(fee.status)}`}>{statusLabel(fee.status)}</span>
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

export default StudentFinancialDetailsPage;
