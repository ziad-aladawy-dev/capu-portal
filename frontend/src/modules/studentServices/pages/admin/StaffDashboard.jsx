import { useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { LayoutDashboard, ClipboardList, UserCheck, Clock, Briefcase, CheckCircle, AlertCircle, Banknote, TrendingUp, ArrowUpRight, ArrowDownRight, Activity } from "lucide-react";
import PageHeader from "../../../../core/components/PageHeader";
import { useStaffDashboard } from "../../hooks/useStaffDashboard";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import EmptyState from "../../../../core/components/EmptyState";
import ErrorMessage from "../../../../core/components/ErrorMessage";
import StatusBadge from "../../../../core/components/RequestStatusBadge";
import { fmtAmount } from "../../../../core/services/treasuryService";
import { getLocalized } from "../../../../core/utils/getLocalized";
import "../../styles/admin/StaffDashboard.css";

const TrendSparkline = ({ data, height = 60, width = 200 }) => {
  if (!data || data.length < 2) return null;
  const max = Math.max(...data.map(d => d.count), 1);
  const points = data.map((d, i) => {
    const x = (i / (data.length - 1)) * width;
    const y = height - (d.count / max) * (height - 10) - 5;
    return `${x},${y}`;
  }).join(" ");
  return (
    <svg viewBox={`0 0 ${width} ${height}`} className="sd-trend-svg">
      <polyline fill="none" stroke="var(--color-gold, #c9a84c)" strokeWidth="2" points={points} />
      {data.map((d, i) => {
        const x = (i / (data.length - 1)) * width;
        const y = height - (d.count / max) * (height - 10) - 5;
        return <circle key={i} cx={x} cy={y} r="2" fill="var(--color-gold, #c9a84c)" />;
      })}
    </svg>
  );
};

const PipelineFunnel = ({ stats }) => {
  const { t } = useTranslation();
  const stages = [
    { key: "pendingRequests", label: "pending", color: "#f59e0b", pct: 1 },
    { key: "awaitingApproval", label: "awaiting_approval", color: "#8b5cf6", pct: 0.75 },
    { key: "completedRequests", label: "completed", color: "#16a34a", pct: 0.5 },
  ];
  return (
    <div className="sd-funnel">
      {stages.map((s) => {
        const val = stats?.[s.key] ?? 0;
        return (
          <div key={s.key} className="sd-funnel-row">
            <span className="sd-funnel-label">{t(s.label)}</span>
            <div className="sd-funnel-bar-track">
              <div className="sd-funnel-bar" style={{ width: `${s.pct * 100}%`, background: s.color }}>
                <span className="sd-funnel-count">{val}</span>
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
};

const StaffDashboard = () => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { stats, recentRequests, assignedToMeCount, trend, loading, error } = useStaffDashboard();

  if (loading && !stats) return <LoadingSpinner fullPage />;
  if (error) return <ErrorMessage message={error} />;

  const totalReq = stats?.totalRequests ?? 0;
  const pendingReq = stats?.pendingRequests ?? 0;
  const completedReq = stats?.completedRequests ?? 0;
  const awaitingReq = stats?.awaitingApproval ?? 0;
  const activeSvcs = stats?.activeServices ?? 0;
  const paidReq = stats?.paidRequests ?? 0;
  const revenue = stats?.totalRevenue ?? 0;

  const sortedRequests = useMemo(() =>
    [...recentRequests].sort((a, b) => {
      if (a.assignedToStaffId && !b.assignedToStaffId) return -1;
      if (!a.assignedToStaffId && b.assignedToStaffId) return 1;
      return new Date(b.submittedAt) - new Date(a.submittedAt);
    }),
    [recentRequests]
  );

  return (
    <div className="sdd-container">
      <PageHeader
        icon={LayoutDashboard}
        title={t("staff_dashboard")}
        subtitle={t("overview_of_student_services")}
        actions={
          <button className="btn-primary" onClick={() => navigate("/admin/student-services/requests")}>
            {t("view_all_requests")}
          </button>
        }
      />

      {/* ── Pulse Section ── */}
      <section className="sdd-section">
        <h2 className="sdd-section-title"><Activity size={16} /> {t("pulse")}</h2>
        <div className="sdd-pulse-grid">
          <div className="sdd-pulse-card sdd-pulse-primary">
            <ClipboardList size={28} />
            <div className="sdd-pulse-body">
              <span className="sdd-pulse-label">{t("total_requests")}</span>
              <span className="sdd-pulse-value">{totalReq}</span>
            </div>
          </div>
          <div className="sdd-pulse-card sdd-pulse-accent" onClick={() => navigate("/admin/student-services/assigned-to-me")} role="button" tabIndex={0} onKeyDown={(e) => e.key === "Enter" && navigate("/admin/student-services/assigned-to-me")}>
            <UserCheck size={28} />
            <div className="sdd-pulse-body">
              <span className="sdd-pulse-label">{t("my_queue")}</span>
              <span className="sdd-pulse-value">{assignedToMeCount}</span>
            </div>
            <ArrowUpRight size={16} className="sdd-pulse-arrow" />
          </div>
          <div className="sdd-pulse-card sdd-pulse-warn">
            <Clock size={28} />
            <div className="sdd-pulse-body">
              <span className="sdd-pulse-label">{t("needs_attention")}</span>
              <span className="sdd-pulse-value">{pendingReq}</span>
            </div>
          </div>
        </div>
      </section>

      {/* ── Pipeline + Trend ── */}
      <div className="sdd-row-2col">
        <section className="sdd-section">
          <h2 className="sdd-section-title"><TrendingUp size={16} /> {t("request_pipeline")}</h2>
          <PipelineFunnel stats={stats} />
        </section>
        <section className="sdd-section">
          <h2 className="sdd-section-title"><Activity size={16} /> {t("request_trend_30d")}</h2>
          {trend.length < 2 ? (
            <EmptyState icon={TrendingUp} title={t("insufficient_data")} />
          ) : (
            <div className="sdd-trend-container">
              <TrendSparkline data={trend} height={80} width={280} />
              <div className="sdd-trend-summary">
                <span className="sdd-trend-stat">
                  {trend.length > 0 ? `${trend[trend.length - 1].count} ${t("this_week").toLowerCase()}` : ""}
                </span>
                <span className="sdd-trend-sub">
                  {trend.length >= 7 && trend[trend.length - 1].count > trend[trend.length - 7].count ? (
                    <><ArrowUpRight size={12} /> +{trend[trend.length - 1].count - trend[trend.length - 7].count}</>
                  ) : trend.length >= 7 ? (
                    <><ArrowDownRight size={12} /> {trend[trend.length - 1].count - trend[trend.length - 7].count}</>
                  ) : null}
                  {" "}{t("vs_last_week")}
                </span>
              </div>
            </div>
          )}
        </section>
      </div>

      {/* ── Performance Cards ── */}
      <section className="sdd-section">
        <h2 className="sdd-section-title"><Briefcase size={16} /> {t("service_performance")}</h2>
        <div className="sdd-perf-grid">
          <div className="sdd-perf-card">
            <Briefcase size={18} /> <div><span>{t("active_services")}</span><h3>{activeSvcs}</h3></div>
          </div>
          <div className="sdd-perf-card">
            <CheckCircle size={18} /> <div><span>{t("completed")}</span><h3>{completedReq}</h3></div>
          </div>
          <div className="sdd-perf-card">
            <AlertCircle size={18} /> <div><span>{t("awaiting_approval")}</span><h3>{awaitingReq}</h3></div>
          </div>
          <div className="sdd-perf-card">
            <Banknote size={18} /> <div><span>{t("paid_requests")}</span><h3>{paidReq}</h3></div>
          </div>
          <div className="sdd-perf-card sdd-perf-revenue">
            <TrendingUp size={18} /> <div><span>{t("revenue")}</span><h3>{fmtAmount(revenue)} <small>EGP</small></h3></div>
          </div>
        </div>
      </section>

      {/* ── Recent Requests ── */}
      <section className="sdd-section">
        <h2 className="sdd-section-title"><ClipboardList size={16} /> {t("recent_requests")}</h2>
        {sortedRequests.length === 0 ? (
          <EmptyState icon={ClipboardList} title={t("no_requests")} />
        ) : (
          <div className="sdd-table-wrapper">
            <table className="sdd-table">
              <thead>
                <tr>
                  <th>{t("request_number")}</th>
                  <th>{t("student")}</th>
                  <th>{t("service")}</th>
                  <th>{t("status")}</th>
                  <th>{t("date")}</th>
                  <th>{t("assigned_to")}</th>
                  <th>{t("actions")}</th>
                </tr>
              </thead>
              <tbody>
                {sortedRequests.map((req) => (
                  <tr key={req.requestId} className={req.assignedToStaffId ? "sdd-row-mine" : ""}>
                    <td className="sdd-number">{req.requestNumber}</td>
                    <td>{getLocalized(req.studentName, i18n.language)}</td>
                    <td>{getLocalized(req.serviceName, i18n.language)}</td>
                    <td><StatusBadge status={req.status} /></td>
                    <td>{new Date(req.submittedAt).toLocaleDateString()}</td>
                    <td>{req.assignedToStaffId ? t("assigned_to_me") : t("unassigned")}</td>
                    <td>
                      <button className="btn-outline" onClick={() => navigate(`/admin/student-services/requests/${req.requestId}`)}>
                        {t("view")}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
};

export default StaffDashboard;
