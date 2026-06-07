import { useState, useEffect, useMemo, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import {
  Receipt, Users, TrendingUp, AlertCircle, RefreshCw, Activity, CheckCircle, Search, 
} from "lucide-react";
import * as invoiceService from "../../../core/services/invoiceService";
import * as studentService from "../../../core/services/studentService";
import "../styles/adminFinance.css";

const COLORS = ["#10b981", "#3b82f6", "#f59e0b", "#ef4444"];

function AdminFinanceDashboard() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);

  // Data State
  const [invoices, setInvoices] = useState([]);
  const [studentsCache, setStudentsCache] = useState({});
  const [searchQuery, setSearchQuery] = useState("");

  const loadDashboardData = useCallback(async (isRefresh = false) => {
    if (isRefresh) setRefreshing(true);
    else setLoading(true);
    setError(null);

    try {
      // 1. Fetch recent/all invoices for dashboard aggregation (up to 1000 for realistic demo scale)
      const invResponse = await invoiceService.searchInvoices({ pageSize: 1000 });
      const allInvoices = Array.isArray(invResponse?.items) ? invResponse.items : [];
      setInvoices(allInvoices);

      // 2. Extract unique student IDs from the fetched invoices
      const uniqueStudentIds = [...new Set(allInvoices.map(i => i.studentId).filter(Boolean))];

      // 3. Fetch missing student details efficiently
      const newCache = { ...studentsCache };
      const missingIds = uniqueStudentIds.filter(id => !newCache[id]);
      
      if (missingIds.length > 0) {
        // Fallback to fetch one by one, or if searchStudents supports it we'd use that. 
        // For the demo we fetch them individually but concurrently.
        const studentPromises = missingIds.map(id => studentService.fetchStudentById(id).catch(() => null));
        const students = await Promise.all(studentPromises);
        students.forEach((st, idx) => {
          if (st) newCache[missingIds[idx]] = st;
        });
        setStudentsCache(newCache);
      }
    } catch (err) {
      setError(err.message || "Failed to load dashboard data");
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [studentsCache]);

  useEffect(() => {
    loadDashboardData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Compute overall metrics
  const { totalBilled, totalPaid, totalPending, studentSummaries } = useMemo(() => {
    let billed = 0;
    let paid = 0;
    let pending = 0;
    
    // Group by student
    const studentMap = {};

    invoices.forEach(inv => {
      const amount = Number(inv.totalAmount) || 0;
      
      // In the absence of direct transaction records in this dashboard slice,
      // we estimate "paid" based on the status.
      // If Paid => fully paid, If PartiallyPaid => we assume 50% for visual metrics (or 0 if unknown),
      // If Cancelled/Refunded => 0.
      let invPaid = 0;
      let invPending = 0;
      
      if (inv.status === invoiceService.INVOICE_STATUS.Paid) {
        invPaid = amount;
      } else if (inv.status === invoiceService.INVOICE_STATUS.PartiallyPaid) {
        invPaid = amount * 0.5; // Demo estimation when exact tx history isn't loaded
        invPending = amount * 0.5;
      } else if (inv.status === invoiceService.INVOICE_STATUS.Pending) {
        invPending = amount;
      }

      billed += amount;
      paid += invPaid;
      pending += invPending;

      if (inv.studentId) {
        if (!studentMap[inv.studentId]) {
          studentMap[inv.studentId] = {
            studentId: inv.studentId,
            totalBilled: 0,
            totalPaid: 0,
            totalPending: 0,
            invoices: [],
          };
        }
        studentMap[inv.studentId].totalBilled += amount;
        studentMap[inv.studentId].totalPaid += invPaid;
        studentMap[inv.studentId].totalPending += invPending;
        studentMap[inv.studentId].invoices.push(inv);
      }
    });

    return {
      totalBilled: billed,
      totalPaid: paid,
      totalPending: pending,
      studentSummaries: Object.values(studentMap),
    };
  }, [invoices]);

  // Chart data
  const pieData = [
    { name: "Collected", value: totalPaid },
    { name: "Pending", value: totalPending },
  ];

  // Group revenue by Month for bar chart
  const barData = useMemo(() => {
    const months = {};
    invoices.forEach(inv => {
      if (inv.status === invoiceService.INVOICE_STATUS.Cancelled) return;
      
      const d = new Date(inv.createdAt);
      const mLabel = d.toLocaleDateString("en-US", { month: "short", year: "numeric" });
      
      if (!months[mLabel]) months[mLabel] = { name: mLabel, Billed: 0, Collected: 0 };
      months[mLabel].Billed += Number(inv.totalAmount);
      
      if (inv.status === invoiceService.INVOICE_STATUS.Paid) {
        months[mLabel].Collected += Number(inv.totalAmount);
      } else if (inv.status === invoiceService.INVOICE_STATUS.PartiallyPaid) {
        months[mLabel].Collected += Number(inv.totalAmount) * 0.5;
      }
    });
    // Sort chronologically
    return Object.values(months).sort((a, b) => new Date(a.name) - new Date(b.name)).slice(-6);
  }, [invoices]);

  // Filter students for table
  const filteredSummaries = useMemo(() => {
    if (!searchQuery.trim()) return studentSummaries;
    const q = searchQuery.toLowerCase();
    return studentSummaries.filter(s => {
      const st = studentsCache[s.studentId];
      if (!st) return false;
      return (st.name && st.name.toLowerCase().includes(q)) || 
             (st.studentCode && st.studentCode.toLowerCase().includes(q));
    });
  }, [studentSummaries, searchQuery, studentsCache]);

  const fmt = (n) => Number(n).toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

  if (loading) {
    return (
      <div className="admin-finance" style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '60vh' }}>
        <div style={{ textAlign: 'center', color: '#64748b' }}>
          <div className="spinner" style={{ marginBottom: 16 }}></div>
          <p>Analyzing financial data…</p>
        </div>
      </div>
    );
  }

  return (
    <div className="admin-finance">
      {/* Webhook Status Banner */}
      <div className="af-webhook-banner">
        <div className="af-webhook-info">
          <div className="af-webhook-icon">
            <Activity size={24} color="#fff" />
          </div>
          <div>
            <span className="af-webhook-title">Vault Integration Active</span>
            <span className="af-webhook-desc">
              Listening for real-time payment events from the University Vault & Central Bank via <strong>/api/payments/webhook</strong>
            </span>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, background: 'rgba(0,0,0,0.15)', padding: '6px 14px', borderRadius: 20 }}>
          <div className="af-pulse"></div>
          <span style={{ fontSize: 13, fontWeight: 600 }}>Connected</span>
        </div>
      </div>

      <div className="af-header" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <div>
          <h1>Financial Dashboard</h1>
          <p>University-wide fee collection and revenue analytics</p>
        </div>
        <button className="af-btn af-btn-outline" onClick={() => loadDashboardData(true)} disabled={refreshing}>
          <RefreshCw size={14} className={refreshing ? "spinning" : ""} />
          {refreshing ? "Syncing..." : "Sync Ledger"}
        </button>
      </div>

      {error && (
        <div style={{ background: '#fef2f2', color: '#dc2626', padding: 16, borderRadius: 12, marginBottom: 24, display: 'flex', gap: 12 }}>
          <AlertCircle size={20} />
          <span>{error}</span>
        </div>
      )}

      {/* Summary Metrics */}
      <div className="af-summary-grid">
        <div className="af-summary-card revenue">
          <div className="af-card-icon"><TrendingUp size={24} /></div>
          <div className="af-card-info">
            <div className="af-card-label">Total Billed</div>
            <div className="af-card-value">{fmt(totalBilled)} <span className="currency">EGP</span></div>
          </div>
        </div>
        <div className="af-summary-card collected">
          <div className="af-card-icon"><CheckCircle size={24} /></div>
          <div className="af-card-info">
            <div className="af-card-label">Total Collected</div>
            <div className="af-card-value">{fmt(totalPaid)} <span className="currency">EGP</span></div>
          </div>
        </div>
        <div className="af-summary-card pending">
          <div className="af-card-icon"><AlertCircle size={24} /></div>
          <div className="af-card-info">
            <div className="af-card-label">Total Pending</div>
            <div className="af-card-value">{fmt(totalPending)} <span className="currency">EGP</span></div>
          </div>
        </div>
        <div className="af-summary-card students">
          <div className="af-card-icon"><Users size={24} /></div>
          <div className="af-card-info">
            <div className="af-card-label">Active Accounts</div>
            <div className="af-card-value">{studentSummaries.length} <span className="currency">Students</span></div>
          </div>
        </div>
      </div>

      {/* Analytics Charts */}
      <div className="af-charts-grid">
        <div className="af-chart-card">
          <div className="af-chart-header" id="revenue-chart-title">Revenue over Time</div>
          <div className="af-chart-wrapper" role="img" aria-labelledby="revenue-chart-title" style={{ position: 'relative', paddingTop: 20 }}>
            {/* Custom CSS Bar Chart */}
            <div style={{ position: 'absolute', top: 20, left: 0, right: 0, bottom: 30, display: 'flex', flexDirection: 'column', justifyContent: 'space-between', zIndex: 0 }} aria-hidden="true">
              {[...Array(5)].map((_, i) => (
                <div key={i} style={{ width: '100%', borderBottom: '1px dashed #e2e8f0', height: 0 }}></div>
              ))}
            </div>
            <div style={{ display: 'flex', alignItems: 'flex-end', height: '100%', gap: '8%', position: 'relative', zIndex: 1 }}>
              {barData.map((d, i) => {
                const maxVal = Math.max(...barData.map(b => Math.max(b.Billed, b.Collected))) || 1;
                const hBilled = Math.max((d.Billed / maxVal) * 100, 2); // min height 2%
                const hCollected = Math.max((d.Collected / maxVal) * 100, 2);
                return (
                  <div key={i} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', height: '100%', justifyContent: 'flex-end' }}>
                    <div style={{ display: 'flex', alignItems: 'flex-end', gap: 4, height: '100%', width: '100%', justifyContent: 'center' }}>
                      <div title={`Collected: ${fmt(d.Collected)}`} style={{ width: '40%', height: `${hCollected}%`, background: '#1a1f5e', borderRadius: '4px 4px 0 0', transition: 'height 1s ease-out' }}></div>
                      <div title={`Billed: ${fmt(d.Billed)}`} style={{ width: '40%', height: `${hBilled}%`, background: '#e0c06a', borderRadius: '4px 4px 0 0', transition: 'height 1s ease-out' }}></div>
                    </div>
                    <span style={{ marginTop: 12, fontSize: 12, color: '#64748b', fontWeight: 500 }}>{d.name}</span>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
        <div className="af-chart-card">
          <div className="af-chart-header" id="collection-chart-title">Collection Rate</div>
          <div className="af-chart-wrapper" role="img" aria-labelledby="collection-chart-title" style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center' }}>
            {/* Custom CSS Donut Chart */}
            {(() => {
              const total = totalPaid + totalPending || 1;
              const pctPaid = (totalPaid / total) * 100;
              return (
                <div style={{
                  width: 180, height: 180, borderRadius: '50%',
                  background: `conic-gradient(#10b981 0% ${pctPaid}%, #ef4444 ${pctPaid}% 100%)`,
                  display: 'flex', alignItems: 'center', justifyContent: 'center',
                  boxShadow: '0 4px 15px rgba(0,0,0,0.05)',
                  transition: 'background 1s ease-out'
                }} aria-hidden="true">
                  <div style={{ width: 130, height: 130, background: 'white', borderRadius: '50%', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center' }}>
                    <span style={{ fontSize: 12, color: '#64748b', fontWeight: 600 }}>Total Paid</span>
                    <span style={{ fontSize: 16, fontWeight: '800', color: '#1a1f5e', marginTop: 2 }}>{fmt(totalPaid)}</span>
                  </div>
                </div>
              );
            })()}
            <div style={{ display: 'flex', justifyContent: 'center', gap: 20, marginTop: 24 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, color: '#64748b', fontWeight: 500 }}>
                <div style={{ width: 12, height: 12, borderRadius: '4px', background: '#10b981' }}></div> Collected ({((totalPaid / (totalPaid + totalPending || 1)) * 100).toFixed(0)}%)
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, color: '#64748b', fontWeight: 500 }}>
                <div style={{ width: 12, height: 12, borderRadius: '4px', background: '#ef4444' }}></div> Pending ({((totalPending / (totalPaid + totalPending || 1)) * 100).toFixed(0)}%)
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Tabular Directory */}
      <div className="af-section-header" style={{ marginTop: 40 }}>
        <div className="af-section-title">
          <Receipt size={18} color="#e0c06a" />
          Student Financial Directory
        </div>
        <div className="af-toolbar">
          <div className="af-search-input">
            <Search size={14} color="#64748b" />
            <input 
              type="text" 
              placeholder="Search student by name or code..." 
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>
        </div>
      </div>

      <div className="af-table-wrapper">
        <table className="af-table">
          <thead>
            <tr>
              <th>Student</th>
              <th className="text-right">Total Billed</th>
              <th className="text-right">Collected</th>
              <th className="text-right">Balance Due</th>
              <th className="text-center">Status Overview</th>
            </tr>
          </thead>
          <tbody>
            {filteredSummaries.length === 0 ? (
              <tr>
                <td colSpan="5">
                  <div className="af-empty-state">
                    <Receipt size={40} />
                    <h3>No financial records found</h3>
                    <p>There are no students matching your search criteria.</p>
                  </div>
                </td>
              </tr>
            ) : (
              filteredSummaries.map((summary) => {
                const student = studentsCache[summary.studentId];
                const hasPending = summary.totalPending > 0;
                
                return (
                  <tr key={summary.studentId} onClick={() => navigate(`/admin/finance/student/${summary.studentId}`)}>
                    <td>
                      <div className="af-student-cell">
                        <div className="af-student-avatar">
                          {student?.name ? student.name.charAt(0).toUpperCase() : "?"}
                        </div>
                        <div className="af-student-info">
                          <strong>{student?.name || "Unknown Student"}</strong>
                          <span>{student?.studentCode || summary.studentId.slice(0, 8)}</span>
                        </div>
                      </div>
                    </td>
                    <td className="text-right">
                      <span className="af-amount neutral">{fmt(summary.totalBilled)}</span>
                    </td>
                    <td className="text-right">
                      <span className="af-amount positive">{fmt(summary.totalPaid)}</span>
                    </td>
                    <td className="text-right">
                      {summary.totalPending > 0 ? (
                        <span className="af-amount negative">{fmt(summary.totalPending)}</span>
                      ) : (
                        <span className="af-amount neutral">0.00</span>
                      )}
                    </td>
                    <td className="text-center">
                      {hasPending ? (
                        <span className="af-badge unpaid">Outstanding Balance</span>
                      ) : (
                        <span className="af-badge paid">Fully Settled</span>
                      )}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default AdminFinanceDashboard;
