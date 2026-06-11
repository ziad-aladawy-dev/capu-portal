import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Package, ClipboardList, Clock, CheckCircle, ArrowRight } from "lucide-react";
import { getAvailableServicesForStudent } from "../../services/studentServicesService";
import { useStudentStatistics } from "../../hooks/useStatistics";
import { useAuth } from "../../../../core/contexts/AuthContext";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import EmptyState from "../../../../core/components/EmptyState";
import ServiceCard from "../../components/ServiceCard";
import "../../styles/student/StudentDashboard.css";

const StudentDashboard = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const { stats: studentStats, loading: statsLoading } = useStudentStatistics();
  const [services, setServices] = useState([]);
  const [loadingServices, setLoadingServices] = useState(true);

  useEffect(() => {
    const load = async () => {
      if (!user?.id) return;
      try {
        const data = await getAvailableServicesForStudent(user.id);
        setServices(Array.isArray(data) ? data : []);
      } catch (err) {
        console.error("Failed to load available services", err);
      } finally {
        setLoadingServices(false);
      }
    };
    load();
  }, [user?.id]);

  const initials = typeof user?.name === "string" && user.name.trim()
    ? user.name.trim().split(/\s+/).map((n) => n[0]).join("").slice(0, 2).toUpperCase()
    : "ST";

  const statsCards = [
    { label: t("available_services"), value: services.length, icon: Package, colorClass: "ic-blue", accentBg: "#2563eb" },
    { label: t("active_requests"), value: studentStats?.activeRequests || 0, icon: ClipboardList, colorClass: "ic-navy", accentBg: "#1a1f5e" },
    { label: t("pending_requests"), value: studentStats?.pendingRequests || 0, icon: Clock, colorClass: "ic-amber", accentBg: "#f59e0b" },
    { label: t("completed_requests"), value: studentStats?.completedRequests || 0, icon: CheckCircle, colorClass: "ic-green", accentBg: "#16a34a" },
  ];

  if ((statsLoading || loadingServices) && !services.length && !studentStats) return <LoadingSpinner />;

  return (
    <div className="std-dashboard-page">
      <div className="std-dashboard-header">
        <div className="std-dashboard-title-box">
          <div className="std-hero-eyebrow">{t("student_portal")}</div>
          <h1>{t("welcome_back_student")}, <em>{user?.name && typeof user.name === "string" ? user.name.split(" ")[0] : ""}</em></h1>
          <div className="std-hero-meta">
            <div className="std-hero-pill">{t("status")}: <b>{t("active_student")}</b></div>
          </div>
        </div>
        <div className="std-welcome-block">
          <div className="std-hero-illus"><div className="std-hero-ring">{initials}</div></div>
          <button className="std-quick-action-pill-btn" onClick={() => navigate("/student/requests")}>
            <span>{t("my_requests")}</span><ArrowRight size={14} className="std-arrow-icon" />
          </button>
        </div>
      </div>

      <div className="std-stats-grid">
        {statsCards.map((stat, idx) => (
          <div key={idx} className="std-stat-card">
            <div className="std-stat-card-accent" style={{ backgroundColor: stat.accentBg }}></div>
            <div className="std-stat-card-top"><span>{stat.label}</span><div className={`std-stat-icon ${stat.colorClass}`}><stat.icon size={16} /></div></div>
            <h2>{stat.value}</h2>
          </div>
        ))}
      </div>

      <div className="std-dashboard-card">
        <div className="std-card-header"><h3>{t("services_catalog")}</h3><span className="std-card-badge">{services.length} {t("services")}</span></div>
        {services.length === 0 ? <EmptyState title={t("no_services_available")} /> : (
          <div className="std-services-grid">{services.map(service => <ServiceCard key={service.id} service={service} />)}</div>
        )}
      </div>
    </div>
  );
};

export default StudentDashboard;