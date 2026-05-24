import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Receipt, Plus, Search, AlertTriangle, X, Trash2, RefreshCw,
} from "lucide-react";
import * as invoiceService from "../../../core/services/invoiceService";
import * as studentService from "../../../core/services/studentService";
import { useUserScope } from "../../../core/hooks/useUserScope";
import "../styles/invoices.css";

const EMPTY_INVOICE = {
  studentId: "",
  currency: "EGP",
  dueAt: "",
  items: [{ feeType: "", sourceModule: "manual", amount: 0, description: "" }],
};

function InvoicesPage() {
  const navigate = useNavigate();
  const { scopedUser, isScoped, scopeToUser, clearScope } = useUserScope();

  const [studentQuery, setStudentQuery] = useState("");
  const [studentResults, setStudentResults] = useState([]);
  const [searchingStudent, setSearchingStudent] = useState(false);
  const [selectedStudent, setSelectedStudent] = useState(null);

  const [invoices, setInvoices] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const [statusFilter, setStatusFilter] = useState("");

  const [modalOpen, setModalOpen] = useState(false);
  const [form, setForm] = useState(EMPTY_INVOICE);
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;
    if (!studentQuery.trim()) {
      setStudentResults([]);
      return () => { cancelled = true; };
    }
    const t = setTimeout(async () => {
      setSearchingStudent(true);
      try {
        const r = await studentService.searchStudents({ search: studentQuery, page: 1, pageSize: 10 });
        if (cancelled) return;
        setStudentResults(r?.items || []);
      } catch {
        if (!cancelled) setStudentResults([]);
      } finally {
        if (!cancelled) setSearchingStudent(false);
      }
    }, 300);
    return () => { cancelled = true; clearTimeout(t); };
  }, [studentQuery]);

  const loadInvoices = useCallback(async (studentId) => {
    if (!studentId) {
      setInvoices([]);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const list = await invoiceService.fetchInvoicesForStudent(studentId);
      setInvoices(Array.isArray(list) ? list : []);
    } catch (err) {
      setError(err.message || "Failed to load invoices");
      setInvoices([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (selectedStudent?.id) loadInvoices(selectedStudent.id);
    else setInvoices([]);
  }, [selectedStudent?.id, loadInvoices]);

  const filtered = useMemo(() => {
    if (statusFilter === "") return invoices;
    return invoices.filter((inv) => String(inv.status) === statusFilter);
  }, [invoices, statusFilter]);

  useEffect(() => {
    if (isScoped && scopedUser?.type === "student" && scopedUser.id !== selectedStudent?.id) {
      setSelectedStudent({ id: scopedUser.id, name: scopedUser.name, code: scopedUser.studentCode });
      setStudentQuery("");
      setStudentResults([]);
    }
  }, [scopedUser?.id, isScoped, selectedStudent?.id]);

  useEffect(() => {
    if (!isScoped && selectedStudent) {
      setSelectedStudent(null);
      setInvoices([]);
    }
  }, [isScoped]);

  const handleSelectStudent = (s) => {
    scopeToUser({ id: s.id, name: s.name, code: s.studentCode, type: "student" });
    setSelectedStudent({ id: s.id, name: s.name, code: s.studentCode });
    setStudentQuery("");
    setStudentResults([]);
  };

  const handleClearStudent = () => {
    clearScope();
    setSelectedStudent(null);
    setInvoices([]);
  };

  const openCreate = () => {
    if (!selectedStudent) {
      setError("Select a student first.");
      return;
    }
    setModalOpen(true);
    setForm({ ...EMPTY_INVOICE, studentId: selectedStudent.id });
    setFormError("");
  };

  const handleItemChange = (idx, field, value) => {
    setForm((prev) => ({
      ...prev,
      items: prev.items.map((item, i) => (i === idx ? { ...item, [field]: value } : item)),
    }));
  };

  const addItem = () => {
    setForm((prev) => ({
      ...prev,
      items: [...prev.items, { feeType: "", sourceModule: "manual", amount: 0, description: "" }],
    }));
  };

  const removeItem = (idx) => {
    setForm((prev) => ({
      ...prev,
      items: prev.items.filter((_, i) => i !== idx),
    }));
  };

  const itemsTotal = useMemo(
    () => form.items.reduce((sum, i) => sum + (Number(i.amount) || 0), 0),
    [form.items]
  );

  const handleCreate = async (e) => {
    e.preventDefault();
    if (form.items.length === 0) {
      setFormError("Add at least one line item.");
      return;
    }
    for (const item of form.items) {
      if (!item.feeType.trim()) {
        setFormError("Every line needs a fee type.");
        return;
      }
      if (!Number.isFinite(Number(item.amount)) || Number(item.amount) <= 0) {
        setFormError("Every line needs a positive amount.");
        return;
      }
    }
    setSaving(true);
    try {
      await invoiceService.createInvoice({
        studentId: form.studentId,
        currency: form.currency || "EGP",
        dueAt: form.dueAt || null,
        items: form.items.map((i) => ({
          amount: Number(i.amount),
          feeType: i.feeType.trim(),
          sourceModule: i.sourceModule || "manual",
          description: i.description || "",
          referenceId: null,
        })),
      });
      setModalOpen(false);
      setForm(EMPTY_INVOICE);
      await loadInvoices(selectedStudent.id);
    } catch (err) {
      setFormError(err.message || "Failed to create invoice");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="invoices-page">
      <div className="invoices-header">
        <div className="invoices-header-left">
          <Receipt size={22} />
          <div>
            <h1>Invoices</h1>
            <p>Issue invoices to students and review their payment status.</p>
          </div>
        </div>
        <div>
          <button
            className="invoices-btn invoices-btn-primary"
            onClick={openCreate}
            disabled={!selectedStudent}
          >
            <Plus size={14} />
            New Invoice
          </button>
        </div>
      </div>

      {error && (
        <div className="invoices-error-banner">
          <AlertTriangle size={16} />
          <span>{error}</span>
          <button
            onClick={() => setError(null)}
            style={{ marginLeft: "auto", background: "transparent", border: "none", cursor: "pointer", color: "#b91c1c" }}
          >
            <X size={14} />
          </button>
        </div>
      )}

      <div className="invoices-toolbar">
        {selectedStudent ? (
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              background: "white",
              border: "1px solid #e5e7eb",
              padding: "8px 12px",
              borderRadius: 8,
            }}
          >
            <strong style={{ fontSize: 13 }}>{selectedStudent.name}</strong>
            <span style={{ fontSize: 11, color: "#6b7280" }}>
              {selectedStudent.code}
            </span>
            <button
              onClick={handleClearStudent}
              style={{ background: "transparent", border: "none", color: "#6b7280", cursor: "pointer" }}
              title="Clear selection"
            >
              <X size={14} />
            </button>
          </div>
        ) : (
          <div style={{ position: "relative", flex: 1, minWidth: 280 }}>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                background: "white",
                border: "1px solid #e5e7eb",
                padding: "8px 12px",
                borderRadius: 8,
              }}
            >
              <Search size={14} />
              <input
                type="text"
                placeholder="Search student by name or code…"
                value={studentQuery}
                onChange={(e) => setStudentQuery(e.target.value)}
                style={{ border: "none", outline: "none", flex: 1, background: "transparent", fontSize: 13 }}
              />
              {searchingStudent && <span style={{ fontSize: 11, color: "#6b7280" }}>…</span>}
            </div>
            {studentResults.length > 0 && (
              <div
                style={{
                  position: "absolute",
                  top: "100%",
                  left: 0,
                  right: 0,
                  background: "white",
                  border: "1px solid #e5e7eb",
                  borderRadius: 8,
                  marginTop: 4,
                  maxHeight: 280,
                  overflowY: "auto",
                  zIndex: 5,
                  boxShadow: "0 8px 20px rgba(15,17,47,0.08)",
                }}
              >
                {studentResults.map((s) => (
                  <button
                    key={s.id}
                    onClick={() => handleSelectStudent(s)}
                    style={{
                      display: "flex",
                      flexDirection: "column",
                      gap: 2,
                      padding: "9px 12px",
                      width: "100%",
                      textAlign: "left",
                      background: "white",
                      border: "none",
                      borderBottom: "1px solid #f0f1f8",
                      cursor: "pointer",
                    }}
                  >
                    <strong style={{ fontSize: 13, color: "#1a1f5e" }}>{s.name}</strong>
                    <span style={{ fontSize: 11, color: "#6b7280" }}>
                      {s.studentCode} · {s.email}
                    </span>
                  </button>
                ))}
              </div>
            )}
          </div>
        )}

        <select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          style={{ minWidth: 160 }}
        >
          <option value="">All statuses</option>
          {Object.entries(invoiceService.INVOICE_STATUS_LABELS).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>

        {selectedStudent && (
          <button
            className="invoices-btn invoices-btn-outline"
            onClick={() => loadInvoices(selectedStudent.id)}
          >
            <RefreshCw size={12} />
            Refresh
          </button>
        )}
      </div>

      {!selectedStudent ? (
        <div className="invoices-empty">
          <Receipt size={40} />
          <h3>Pick a student</h3>
          <p>Search a student above to view their invoices.</p>
        </div>
      ) : loading ? (
        <div className="invoices-loading">
          <div className="invoices-spinner" />
          <p>Loading invoices…</p>
        </div>
      ) : filtered.length === 0 ? (
        <div className="invoices-empty">
          <Receipt size={40} />
          <h3>No invoices found</h3>
          <p>
            {invoices.length === 0
              ? "This student has no invoices yet."
              : "No invoices match the current filter."}
          </p>
          {invoices.length === 0 && (
            <button className="invoices-btn invoices-btn-primary" onClick={openCreate}>
              <Plus size={14} />
              Create First Invoice
            </button>
          )}
        </div>
      ) : (
        <div className="invoices-table-wrapper">
          <table className="invoices-table">
            <thead>
              <tr>
                <th>Invoice</th>
                <th>Created</th>
                <th>Due</th>
                <th>Items</th>
                <th>Total</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((inv) => (
                <tr key={inv.id} onClick={() => navigate(`/admin/invoices/${inv.id}`)}>
                  <td>
                    <strong style={{ fontFamily: "Space Mono, monospace", fontSize: 11 }}>
                      {inv.id.slice(0, 8)}…
                    </strong>
                  </td>
                  <td>{new Date(inv.createdAt).toLocaleDateString()}</td>
                  <td>{inv.dueAt ? new Date(inv.dueAt).toLocaleDateString() : "—"}</td>
                  <td>{inv.items?.length ?? 0}</td>
                  <td>
                    <strong>
                      {Number(inv.totalAmount).toFixed(2)} {inv.currency}
                    </strong>
                  </td>
                  <td>
                    <span className={`invoices-badge status-${inv.status}`}>
                      {invoiceService.getInvoiceStatusLabel(inv.status)}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {modalOpen && (
        <div className="invoice-modal-overlay" onClick={() => setModalOpen(false)}>
          <div className="invoice-modal" onClick={(e) => e.stopPropagation()}>
            <div className="invoice-modal-header">
              <h2>New Invoice — {selectedStudent?.name}</h2>
              <button className="invoice-modal-close" onClick={() => setModalOpen(false)}>
                <X size={16} />
              </button>
            </div>
            <form onSubmit={handleCreate}>
              <div className="invoice-modal-body">
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                  <div className="invoice-form-group">
                    <label>Currency</label>
                    <select
                      className="invoice-form-select"
                      value={form.currency}
                      onChange={(e) => setForm((p) => ({ ...p, currency: e.target.value }))}
                    >
                      <option value="EGP">EGP</option>
                      <option value="USD">USD</option>
                      <option value="EUR">EUR</option>
                      <option value="GBP">GBP</option>
                    </select>
                  </div>
                  <div className="invoice-form-group">
                    <label>Due Date (optional)</label>
                    <input
                      type="date"
                      className="invoice-form-input"
                      value={form.dueAt}
                      onChange={(e) => setForm((p) => ({ ...p, dueAt: e.target.value }))}
                    />
                  </div>
                </div>

                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: 6 }}>
                  <strong style={{ fontSize: 13 }}>Line items</strong>
                  <button
                    type="button"
                    className="invoices-btn invoices-btn-outline"
                    style={{ padding: "5px 10px", fontSize: 11 }}
                    onClick={addItem}
                  >
                    <Plus size={11} />
                    Add Line
                  </button>
                </div>

                <div className="invoice-items">
                  {form.items.map((item, idx) => (
                    <div key={idx} className="invoice-item-row">
                      <input
                        type="text"
                        className="invoice-form-input"
                        placeholder="Fee type (e.g. Tuition, Lab)"
                        value={item.feeType}
                        onChange={(e) => handleItemChange(idx, "feeType", e.target.value)}
                      />
                      <input
                        type="text"
                        className="invoice-form-input"
                        placeholder="Source module"
                        value={item.sourceModule}
                        onChange={(e) => handleItemChange(idx, "sourceModule", e.target.value)}
                      />
                      <input
                        type="number"
                        step="0.01"
                        min="0"
                        className="invoice-form-input"
                        placeholder="Amount"
                        value={item.amount}
                        onChange={(e) => handleItemChange(idx, "amount", e.target.value)}
                      />
                      <button
                        type="button"
                        className="invoices-btn invoices-btn-danger"
                        style={{ padding: "5px 7px" }}
                        onClick={() => removeItem(idx)}
                        disabled={form.items.length <= 1}
                      >
                        <Trash2 size={12} />
                      </button>
                    </div>
                  ))}
                </div>

                <div style={{ display: "flex", justifyContent: "space-between", paddingTop: 8, borderTop: "1px dashed #e5e7eb" }}>
                  <span style={{ color: "#6b7280", fontSize: 13 }}>Total</span>
                  <strong style={{ color: "#1a1f5e" }}>
                    {itemsTotal.toFixed(2)} {form.currency}
                  </strong>
                </div>

                {formError && <span className="invoice-form-error">{formError}</span>}
              </div>
              <div className="invoice-modal-footer">
                <button
                  type="button"
                  className="invoices-btn invoices-btn-outline"
                  onClick={() => setModalOpen(false)}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="invoices-btn invoices-btn-primary"
                  disabled={saving}
                >
                  {saving ? "Issuing…" : "Issue Invoice"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

export default InvoicesPage;
