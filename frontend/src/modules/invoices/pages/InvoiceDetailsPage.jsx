import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  Receipt, ArrowLeft, AlertTriangle, X, Plus, CreditCard, Ban,
} from "lucide-react";
import * as invoiceService from "../../../core/services/invoiceService";
import * as paymentService from "../../../core/services/paymentService";
import "../styles/invoices.css";

const EMPTY_PAYMENT = {
  provider: "manual",
  providerTransactionId: "",
  status: paymentService.PAYMENT_TX_STATUS.Succeeded,
  amount: 0,
  rawPayloadJson: "{}",
  idempotencyKey: "",
};

function InvoiceDetailsPage() {
  const { invoiceId } = useParams();
  const navigate = useNavigate();

  const [invoice, setInvoice] = useState(null);
  const [transactions, setTransactions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [cancelModalOpen, setCancelModalOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState("");
  const [cancelling, setCancelling] = useState(false);

  const [payModalOpen, setPayModalOpen] = useState(false);
  const [payForm, setPayForm] = useState(EMPTY_PAYMENT);
  const [payFormError, setPayFormError] = useState("");
  const [paying, setPaying] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [inv, txs] = await Promise.all([
        invoiceService.fetchInvoice(invoiceId),
        paymentService.fetchTransactionsForInvoice(invoiceId).catch(() => []),
      ]);
      setInvoice(inv);
      setTransactions(Array.isArray(txs) ? txs : []);
    } catch (err) {
      setError(err.message || "Failed to load invoice");
    } finally {
      setLoading(false);
    }
  }, [invoiceId]);

  useEffect(() => {
    load();
  }, [load]);

  const handleCancel = async () => {
    if (!cancelReason.trim()) {
      setError("Cancellation reason is required.");
      return;
    }
    setCancelling(true);
    try {
      await invoiceService.cancelInvoice(invoiceId, cancelReason.trim());
      setCancelModalOpen(false);
      setCancelReason("");
      await load();
    } catch (err) {
      setError(err.message || "Failed to cancel invoice");
    } finally {
      setCancelling(false);
    }
  };

  const handleRecordPayment = async (e) => {
    e.preventDefault();
    if (!Number.isFinite(Number(payForm.amount)) || Number(payForm.amount) <= 0) {
      setPayFormError("Amount must be positive.");
      return;
    }
    if (!payForm.providerTransactionId.trim()) {
      setPayFormError("Provider transaction ID is required.");
      return;
    }
    if (!payForm.idempotencyKey.trim()) {
      setPayFormError("Idempotency key is required.");
      return;
    }
    setPaying(true);
    try {
      await paymentService.recordPayment({
        invoiceId,
        provider: payForm.provider.trim() || "manual",
        providerTransactionId: payForm.providerTransactionId.trim(),
        status: Number(payForm.status),
        amount: Number(payForm.amount),
        rawPayloadJson: payForm.rawPayloadJson || "{}",
        idempotencyKey: payForm.idempotencyKey.trim(),
      });
      setPayModalOpen(false);
      setPayForm(EMPTY_PAYMENT);
      await load();
    } catch (err) {
      setPayFormError(err.message || "Failed to record payment");
    } finally {
      setPaying(false);
    }
  };

  const paidTotal = transactions
    .filter((t) => t.status === paymentService.PAYMENT_TX_STATUS.Succeeded)
    .reduce((sum, t) => sum + Number(t.amount), 0);

  const balance = invoice ? Number(invoice.totalAmount) - paidTotal : 0;
  const isCancelled = invoice?.status === invoiceService.INVOICE_STATUS.Cancelled;
  const isPaid = invoice?.status === invoiceService.INVOICE_STATUS.Paid;

  if (loading) {
    return (
      <div className="invoice-details-page">
        <div className="invoices-loading">
          <div className="invoices-spinner" />
          <p>Loading invoice…</p>
        </div>
      </div>
    );
  }

  if (error && !invoice) {
    return (
      <div className="invoice-details-page">
        <button className="invoice-back" onClick={() => navigate("/admin/invoices")}>
          <ArrowLeft size={14} /> Back
        </button>
        <div className="invoices-error">
          <AlertTriangle size={32} color="#dc2626" />
          <h3>Failed to load invoice</h3>
          <p>{error}</p>
          <button className="invoices-btn invoices-btn-outline" onClick={load}>
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="invoice-details-page">
      <button className="invoice-back" onClick={() => navigate("/admin/invoices")}>
        <ArrowLeft size={14} /> Back to Invoices
      </button>

      <div className="invoice-details-header">
        <div className="invoice-details-header-left">
          <Receipt size={22} />
          <div>
            <h1>Invoice {invoice.id.slice(0, 8)}…</h1>
            <p>
              <span className={`invoices-badge status-${invoice.status}`}>
                {invoiceService.getInvoiceStatusLabel(invoice.status)}
              </span>{" "}
              · Issued {new Date(invoice.createdAt).toLocaleString()}
            </p>
          </div>
        </div>
        <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
          {!isCancelled && !isPaid && (
            <button
              className="invoices-btn invoices-btn-primary"
              onClick={() => {
                setPayForm({ ...EMPTY_PAYMENT, amount: Number(balance) || 0 });
                setPayModalOpen(true);
              }}
            >
              <CreditCard size={13} />
              Record Payment
            </button>
          )}
          {!isCancelled && (
            <button
              className="invoices-btn invoices-btn-danger"
              onClick={() => setCancelModalOpen(true)}
            >
              <Ban size={13} />
              Cancel Invoice
            </button>
          )}
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

      <div className="invoice-details-grid">
        <div>
          <div className="invoice-detail-card">
            <h3><Receipt size={15} /> Items</h3>
            <table className="invoice-line-table">
              <thead>
                <tr>
                  <th>Fee Type</th>
                  <th>Source</th>
                  <th>Description</th>
                  <th style={{ textAlign: "right" }}>Amount</th>
                </tr>
              </thead>
              <tbody>
                {(invoice.items || []).map((item) => (
                  <tr key={item.id}>
                    <td><strong>{item.feeType}</strong></td>
                    <td>{item.sourceModule}</td>
                    <td>{item.description || "—"}</td>
                    <td style={{ textAlign: "right" }}>
                      {Number(item.amount).toFixed(2)} {invoice.currency}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="invoice-detail-card" style={{ marginTop: 16 }}>
            <h3><CreditCard size={15} /> Payment Transactions</h3>
            {transactions.length === 0 ? (
              <p style={{ fontSize: 13, color: "#6b7280" }}>
                No payment transactions recorded for this invoice yet.
              </p>
            ) : (
              <table className="invoice-tx-table">
                <thead>
                  <tr>
                    <th>Provider</th>
                    <th>Provider Tx ID</th>
                    <th>When</th>
                    <th>Status</th>
                    <th style={{ textAlign: "right" }}>Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {transactions.map((tx) => (
                    <tr key={tx.id}>
                      <td><strong>{tx.provider}</strong></td>
                      <td style={{ fontFamily: "Space Mono, monospace", fontSize: 11 }}>
                        {tx.providerTransactionId}
                      </td>
                      <td>{new Date(tx.createdAt).toLocaleString()}</td>
                      <td>
                        <span className={`invoice-tx-badge status-${tx.status}`}>
                          {paymentService.getPaymentStatusLabel(tx.status)}
                        </span>
                      </td>
                      <td style={{ textAlign: "right" }}>
                        {Number(tx.amount).toFixed(2)} {invoice.currency}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>

        <div>
          <div className="invoice-summary">
            <div className="total-label">Total</div>
            <div>
              <span className="total-value">{Number(invoice.totalAmount).toFixed(2)}</span>
              <span className="total-currency">{invoice.currency}</span>
            </div>
            <div className="summary-row">
              <span>Paid</span>
              <strong>{paidTotal.toFixed(2)} {invoice.currency}</strong>
            </div>
            <div className="summary-row">
              <span>Balance</span>
              <strong>{balance.toFixed(2)} {invoice.currency}</strong>
            </div>
          </div>

          <div className="invoice-detail-card" style={{ marginTop: 16 }}>
            <h3>Metadata</h3>
            <div className="invoice-meta-grid">
              <div>
                <span>Student</span>
                <strong style={{ fontFamily: "Space Mono, monospace", fontSize: 11 }}>
                  {invoice.studentId}
                </strong>
              </div>
              <div>
                <span>Created</span>
                <strong>{new Date(invoice.createdAt).toLocaleString()}</strong>
              </div>
              <div>
                <span>Due</span>
                <strong>{invoice.dueAt ? new Date(invoice.dueAt).toLocaleString() : "—"}</strong>
              </div>
              <div>
                <span>Items</span>
                <strong>{invoice.items?.length ?? 0}</strong>
              </div>
            </div>
          </div>
        </div>
      </div>

      {cancelModalOpen && (
        <div className="invoice-modal-overlay" onClick={() => setCancelModalOpen(false)}>
          <div className="invoice-modal" onClick={(e) => e.stopPropagation()}>
            <div className="invoice-modal-header">
              <h2>Cancel Invoice</h2>
              <button className="invoice-modal-close" onClick={() => setCancelModalOpen(false)}>
                <X size={16} />
              </button>
            </div>
            <div className="invoice-modal-body">
              <p style={{ fontSize: 13, color: "#6b7280", margin: 0 }}>
                Provide a brief reason for cancellation. This will be persisted with the invoice.
              </p>
              <div className="invoice-form-group">
                <label>Reason</label>
                <textarea
                  rows={4}
                  className="invoice-form-textarea"
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  placeholder="e.g. Duplicate of invoice X / billed in error"
                  maxLength={500}
                />
              </div>
            </div>
            <div className="invoice-modal-footer">
              <button
                className="invoices-btn invoices-btn-outline"
                onClick={() => setCancelModalOpen(false)}
                disabled={cancelling}
              >
                Cancel
              </button>
              <button
                className="invoices-btn invoices-btn-danger"
                onClick={handleCancel}
                disabled={cancelling}
              >
                {cancelling ? "Cancelling…" : "Confirm Cancellation"}
              </button>
            </div>
          </div>
        </div>
      )}

      {payModalOpen && (
        <div className="invoice-modal-overlay" onClick={() => setPayModalOpen(false)}>
          <div className="invoice-modal" onClick={(e) => e.stopPropagation()}>
            <div className="invoice-modal-header">
              <h2>Record Payment Transaction</h2>
              <button className="invoice-modal-close" onClick={() => setPayModalOpen(false)}>
                <X size={16} />
              </button>
            </div>
            <form onSubmit={handleRecordPayment}>
              <div className="invoice-modal-body">
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                  <div className="invoice-form-group">
                    <label>Provider</label>
                    <input
                      type="text"
                      className="invoice-form-input"
                      value={payForm.provider}
                      onChange={(e) => setPayForm((p) => ({ ...p, provider: e.target.value }))}
                      placeholder="manual / paymob / fawry…"
                    />
                  </div>
                  <div className="invoice-form-group">
                    <label>Provider Tx ID</label>
                    <input
                      type="text"
                      className="invoice-form-input"
                      value={payForm.providerTransactionId}
                      onChange={(e) =>
                        setPayForm((p) => ({ ...p, providerTransactionId: e.target.value }))
                      }
                      placeholder="External transaction reference"
                    />
                  </div>
                </div>

                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                  <div className="invoice-form-group">
                    <label>Status</label>
                    <select
                      className="invoice-form-select"
                      value={payForm.status}
                      onChange={(e) => setPayForm((p) => ({ ...p, status: Number(e.target.value) }))}
                    >
                      {Object.entries(paymentService.PAYMENT_TX_STATUS_LABELS).map(
                        ([value, label]) => (
                          <option key={value} value={value}>
                            {label}
                          </option>
                        )
                      )}
                    </select>
                  </div>
                  <div className="invoice-form-group">
                    <label>Amount ({invoice.currency})</label>
                    <input
                      type="number"
                      step="0.01"
                      min="0.01"
                      className="invoice-form-input"
                      value={payForm.amount}
                      onChange={(e) => setPayForm((p) => ({ ...p, amount: e.target.value }))}
                    />
                  </div>
                </div>

                <div className="invoice-form-group">
                  <label>Idempotency Key</label>
                  <input
                    type="text"
                    className="invoice-form-input"
                    value={payForm.idempotencyKey}
                    onChange={(e) => setPayForm((p) => ({ ...p, idempotencyKey: e.target.value }))}
                    placeholder="Unique key — retried deliveries with the same key dedupe"
                  />
                </div>

                <div className="invoice-form-group">
                  <label>Raw Payload (JSON, optional)</label>
                  <textarea
                    rows={3}
                    className="invoice-form-textarea"
                    value={payForm.rawPayloadJson}
                    onChange={(e) => setPayForm((p) => ({ ...p, rawPayloadJson: e.target.value }))}
                    placeholder='{"providerNotes": "…"}'
                  />
                </div>

                {payFormError && <span className="invoice-form-error">{payFormError}</span>}
              </div>
              <div className="invoice-modal-footer">
                <button
                  type="button"
                  className="invoices-btn invoices-btn-outline"
                  onClick={() => setPayModalOpen(false)}
                  disabled={paying}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="invoices-btn invoices-btn-primary"
                  disabled={paying}
                >
                  {paying ? "Recording…" : "Record Payment"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

export default InvoiceDetailsPage;
