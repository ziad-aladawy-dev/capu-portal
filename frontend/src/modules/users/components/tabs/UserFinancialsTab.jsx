import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { Receipt, ExternalLink, AlertCircle, Calendar } from "lucide-react";
import * as invoiceService from "../../../../core/services/invoiceService";
import { getInvoiceStatusLabel } from "../../../../core/services/invoiceService";

const STATUS_COLORS = {
  0: { bg: "rgba(245,158,11,0.12)", color: "#d97706" },
  1: { bg: "rgba(59,130,246,0.12)", color: "#2563eb" },
  2: { bg: "rgba(22,163,74,0.12)", color: "#16a34a" },
  3: { bg: "rgba(220,38,38,0.1)", color: "#dc2626" },
  4: { bg: "rgba(107,114,128,0.1)", color: "#6b7280" },
};

function UserFinancialsTab({ userId, userType }) {
  const navigate = useNavigate();
  const [invoices, setInvoices] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (userType !== "student") {
      setLoading(false);
      setInvoices([]);
      return;
    }
    setLoading(true);
    invoiceService.fetchInvoicesForStudent(userId)
      .then((data) => {
        setInvoices(Array.isArray(data) ? data : []);
      })
      .catch((err) => {
        setError(err.message || "Failed to load invoices");
        setInvoices([]);
      })
      .finally(() => setLoading(false));
  }, [userId, userType]);

  const formatDate = (date) => {
    if (!date) return "—";
    return new Date(date).toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" });
  };

  const formatCurrency = (amount) => {
    return (amount || 0).toLocaleString("en-US", { minimumFractionDigits: 2 });
  };

  if (userType !== "student") {
    return (
      <div style={{ textAlign: "center", padding: "40px 20px", color: "#9ca3af" }}>
        <Receipt size={32} style={{ opacity: 0.3, marginBottom: 8 }} />
        <p style={{ fontSize: 13 }}>Financials are only available for students.</p>
      </div>
    );
  }

  if (loading) {
    return <div style={{ padding: 40, textAlign: "center", color: "#9ca3af" }}>Loading invoices…</div>;
  }

  if (error) {
    return (
      <div style={{ display: "flex", alignItems: "center", gap: 8, padding: 12, background: "rgba(220,38,38,0.08)", borderRadius: 8, color: "#dc2626", fontSize: 12 }}>
        <AlertCircle size={14} /> {error}
      </div>
    );
  }

  if (invoices.length === 0) {
    return (
      <div style={{ textAlign: "center", padding: "40px 20px", color: "#9ca3af" }}>
        <Receipt size={32} style={{ opacity: 0.3, marginBottom: 8 }} />
        <p style={{ fontSize: 13 }}>No invoices found for this student.</p>
      </div>
    );
  }

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
        <div>
          <h3 className="section-title" style={{ margin: 0 }}>Invoices & Payments</h3>
          <p style={{ fontSize: 11, color: "#6b7280", margin: "4px 0 0" }}>{invoices.length} invoice{invoices.length > 1 ? "s" : ""}</p>
        </div>
        <button
          onClick={() => navigate("/admin/invoices")}
          style={{
            display: "inline-flex", alignItems: "center", gap: 6,
            padding: "7px 12px", borderRadius: 8, border: "none",
            background: "#f0f1f8", color: "#1a1f5e",
            fontSize: 11, fontWeight: 700, cursor: "pointer",
          }}
        >
          <ExternalLink size={13} /> All Invoices
        </button>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        <div style={{
          display: "grid", gridTemplateColumns: "1fr 80px 100px 120px 40px",
          gap: 8, padding: "8px 12px", fontSize: 10, fontWeight: 800,
          color: "#6b7280", textTransform: "uppercase",
          borderBottom: "1px solid #edf0f5",
        }}>
          <span>Description</span>
          <span>Amount</span>
          <span>Status</span>
          <span>Date</span>
          <span></span>
        </div>
        {invoices.map((inv) => {
          const sc = STATUS_COLORS[inv.status] || { bg: "rgba(107,114,128,0.1)", color: "#6b7280" };
          return (
            <div
              key={inv.id}
              style={{
                display: "grid", gridTemplateColumns: "1fr 80px 100px 120px 40px",
                gap: 8, alignItems: "center", padding: "8px 12px",
                borderRadius: 8, cursor: "pointer",
                transition: "background 0.15s",
              }}
              onMouseEnter={(e) => e.currentTarget.style.background = "#f8f9fb"}
              onMouseLeave={(e) => e.currentTarget.style.background = "transparent"}
              onClick={() => navigate(`/admin/invoices/${inv.id}`)}
            >
              <span style={{ fontSize: 12, fontWeight: 500 }}>{inv.description || `Invoice #${inv.id}`}</span>
              <span style={{ fontSize: 12, fontWeight: 700, fontFamily: "monospace" }}>{formatCurrency(inv.amount)}</span>
              <span style={{
                fontSize: 10, fontWeight: 700, padding: "3px 8px", borderRadius: 6,
                background: sc.bg, color: sc.color, textAlign: "center", width: "fit-content",
              }}>
                {getInvoiceStatusLabel(inv.status)}
              </span>
              <span style={{ fontSize: 11, color: "#6b7280", display: "flex", alignItems: "center", gap: 4 }}>
                <Calendar size={10} /> {formatDate(inv.dueAt || inv.createdAt)}
              </span>
              <ExternalLink size={11} style={{ color: "#9ca3af" }} />
            </div>
          );
        })}
      </div>
    </div>
  );
}

export default UserFinancialsTab;
