import { useState, useEffect, useCallback } from "react";
import { Receipt, Search, Filter, AlertCircle, ChevronLeft, ChevronRight } from "lucide-react";
import * as paymentService from "../../../core/services/paymentService";
import "../styles/transactions.css";

const PAGE_SIZE = 20;

function TransactionsPage() {
  const [transactions, setTransactions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = { page, pageSize: PAGE_SIZE };
      if (statusFilter) params.status = statusFilter;
      if (search.trim()) params.search = search.trim();
      const data = await paymentService.searchTransactions(params);
      setTransactions(Array.isArray(data?.items) ? data.items : []);
      setTotalCount(data?.totalCount ?? 0);
      setTotalPages(data?.totalPages ?? 0);
    } catch (err) {
      setError(err.message || "Failed to load transactions");
    } finally {
      setLoading(false);
    }
  }, [page, statusFilter, search]);

  useEffect(() => { load(); }, [load]);

  const handleSearch = (e) => {
    setSearch(e.target.value);
    setPage(1);
  };

  const handleStatusFilter = (e) => {
    setStatusFilter(e.target.value);
    setPage(1);
  };

  const getStatusBadge = (status) => {
    const map = {
      0: { label: "Pending", className: "tx-badge-warning" },
      1: { label: "Succeeded", className: "tx-badge-success" },
      2: { label: "Failed", className: "tx-badge-danger" },
      3: { label: "Refunded", className: "tx-badge-info" },
    };
    const s = map[status] || { label: "Unknown", className: "tx-badge-outline" };
    return <span className={`tx-badge ${s.className}`}>{s.label}</span>;
  };

  const shortId = (id) => id ? id.slice(0, 12) : "—";

  return (
    <div className="tx-page">
      <div className="tx-header">
        <div className="tx-header-left">
          <Receipt size={24} />
          <div>
            <h1>Payment Transactions</h1>
            <p>View and manage all payment transactions across the institution</p>
          </div>
        </div>
        <div className="tx-pagination-info" style={{ fontSize: 13, color: "#6b7280" }}>
          {totalCount > 0 && `${totalCount} transaction${totalCount !== 1 ? "s" : ""}`}
        </div>
      </div>

      {error && (
        <div className="tx-alert tx-alert-error">
          <AlertCircle size={16} /> {error}
        </div>
      )}

      <div className="tx-toolbar">
        <div className="tx-search-bar">
          <Search size={16} />
          <input
            type="text"
            placeholder="Search by provider, transaction ID..."
            value={search}
            onChange={handleSearch}
          />
        </div>
        <div className="tx-filter-group">
          <Filter size={16} />
          <select className="form-select" value={statusFilter} onChange={handleStatusFilter}>
            <option value="">All Statuses</option>
            <option value="0">Pending</option>
            <option value="1">Succeeded</option>
            <option value="2">Failed</option>
            <option value="3">Refunded</option>
          </select>
        </div>
      </div>

      {loading ? (
        <div className="tx-loading">
          <div className="tx-spinner" />
          <p>Loading transactions...</p>
        </div>
      ) : transactions.length === 0 ? (
        <div className="tx-empty">
          <Receipt size={48} />
          <h3>{search || statusFilter ? "No transactions match your filters" : "No transactions yet"}</h3>
          <p>Payment transactions will appear here once payments are processed</p>
        </div>
      ) : (
        <>
          <div className="tx-table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>Transaction ID</th>
                  <th>Provider</th>
                  <th>Provider Tx ID</th>
                  <th>Invoice</th>
                  <th className="text-right">Amount</th>
                  <th>Status</th>
                  <th>Date</th>
                </tr>
              </thead>
              <tbody>
                {transactions.map((tx) => (
                  <tr key={tx.id}>
                    <td className="tx-muted" style={{ fontFamily: "monospace", fontSize: 12 }}>
                      {shortId(tx.id)}
                    </td>
                    <td>{tx.provider || "—"}</td>
                    <td className="tx-muted" style={{ fontSize: 12 }}>
                      {tx.providerTransactionId || "—"}
                    </td>
                    <td className="tx-muted" style={{ fontFamily: "monospace", fontSize: 12 }}>
                      {shortId(tx.invoiceId)}
                    </td>
                    <td className="tx-amount" style={{ fontWeight: 600 }}>
                      {Number(tx.amount).toLocaleString("en-US", { minimumFractionDigits: 2 })}
                    </td>
                    <td>{getStatusBadge(tx.status)}</td>
                    <td className="tx-muted">
                      {tx.createdAt ? new Date(tx.createdAt).toLocaleDateString() : "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="tx-pagination" style={{ display: "flex", justifyContent: "center", alignItems: "center", gap: 12, marginTop: 20 }}>
              <button
                className="btn-icon"
                disabled={page <= 1}
                onClick={() => setPage(p => Math.max(1, p - 1))}
              >
                <ChevronLeft size={16} />
              </button>
              <span style={{ fontSize: 13, color: "#6b7280" }}>
                Page {page} of {totalPages}
              </span>
              <button
                className="btn-icon"
                disabled={page >= totalPages}
                onClick={() => setPage(p => Math.min(totalPages, p + 1))}
              >
                <ChevronRight size={16} />
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

export default TransactionsPage;
