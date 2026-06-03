import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { Package, ClipboardList, Clock, CheckCircle, Eye } from "lucide-react";
import { getAvailableServicesForStudent } from "../../services/studentServicesService";
import { useStudentStatistics } from "../../hooks/useStatistics";
import { useAuth } from "../../../../core/contexts/AuthContext";
import LoadingSpinner from "../../components/LoadingSpinner";
import EmptyState from "../../components/EmptyState";
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
    const loadServices = async () => {
      if (!user?.id) return;
      setLoadingServices(true);
      try {
        const data = await getAvailableServicesForStudent(user.id);
        setServices(Array.isArray(data) ? data : []);
      } catch (err) {
        console.error("Failed to load available services", err);
      } finally {
        setLoadingServices(false);
      }
    };
    loadServices();
  }, [user?.id]);

  const statsCards = [
    { label: t("available_services"), value: services.length, icon: Package, color: "blue" },
    { label: t("active_requests"), value: studentStats?.activeRequests || 0, icon: ClipboardList, color: "navy" },
    { label: t("pending_requests"), value: studentStats?.pendingRequests || 0, icon: Clock, color: "orange" },
    { label: t("completed_requests"), value: studentStats?.completedRequests || 0, icon: CheckCircle, color: "green" },
  ];

  if ((statsLoading || loadingServices) && !services.length && !studentStats) return <LoadingSpinner />;

  return (
    <div className="std-dashboard">
      <div className="std-welcome">
        <h1>{t("welcome_back_student")}</h1>
        <p>{t("student_dashboard_subtitle")}</p>
      </div>
      <div className="std-stats-grid">
        {statsCards.map((stat, idx) => (
          <div key={idx} className={`std-stat-card ${stat.color}`}>
            <div className="std-stat-icon"><stat.icon size={22} /></div>
            <div className="std-stat-content">
              <span>{stat.label}</span>
              <h3>{stat.value}</h3>
            </div>
          </div>
        ))}
      </div>
      <div className="std-services-header">
        <h2>{t("services_catalog")}</h2>
        <button className="std-view-requests" onClick={() => navigate("/student/requests")}>
          <Eye size={16} /> {t("my_requests")}
        </button>
      </div>
      {services.length === 0 ? (
        <EmptyState message={t("no_services_available")} />
      ) : (
        <div className="std-services-grid">
          {services.map(service => (
            <ServiceCard key={service.id} service={service} />
          ))}
        </div>
      )}
    </div>
  );
};

export default StudentDashboard;