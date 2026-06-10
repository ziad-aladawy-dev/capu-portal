import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  Plus, BarChart3, BookOpen, Calendar, FileText, AlertCircle, Receipt,
  Package, ClipboardList, Clock, CheckCircle, Eye, Bell
} from "lucide-react";
import { useAuth } from "../../../core/auth/useAuth";
import { getAvailableServicesForStudent } from "../../studentServices/services/studentServicesService";
import { useStudentStatistics } from "../../studentServices/hooks/useStatistics";
import * as courseService from "../../../core/services/courseService";
import api from "../../../core/api/apiClient";
import ServiceCard from "../components/ServiceCard";
import LoadingSpinner from "../../studentServices/components/LoadingSpinner";
import "../styles/studentDashboard.css";

function StudentDashboard() {
  const { t } = useTranslation();
  const { user, activeScope } = useAuth();
  const { stats: requestStats, loading: statsLoading } = useStudentStatistics();
  const [error, setError] = useState(null);
  const [services, setServices] = useState([]);
  const [loadingServices, setLoadingServices] = useState(true);
  const [offeringCount, setOfferingCount] = useState(null);
  const [courseCount, setCourseCount] = useState(null);
  const [totalCredits, setTotalCredits] = useState(null);
  const [semesterName, setSemesterName] = useState(null);
  const [academicLoading, setAcademicLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    const fetchAcademicData = async () => {
      setAcademicLoading(true);

      let nodeId = activeScope?.structural?.nodeId;
      let semId = activeScope?.temporal?.semesterId;

      if (!nodeId || !semId) {
        try {
          const scopeNode = JSON.parse(localStorage.getItem("capu_selected_scope_node"));
          const semester = JSON.parse(localStorage.getItem("capu_selected_semester"));
          if (!nodeId && scopeNode?.id) nodeId = scopeNode.id;
          if (!semId && semester?.id) semId = semester.id;
          if (semester?.name && !cancelled) setSemesterName(semester.name);
        } catch { }
      }

      if (!nodeId || !semId) {
        if (!cancelled) { setAcademicLoading(false); }
        return;
      }

      try {
        const semester = JSON.parse(localStorage.getItem("capu_selected_semester"));
        if (semester?.name && !cancelled) setSemesterName(semester.name);

        const resp = await api.get(`/course-offerings/node/${nodeId}/semester/${semId}`);
        const offerings = Array.isArray(resp.data) ? resp.data : [];

        if (cancelled) return;
        setOfferingCount(offerings.length);

        const courseIds = [...new Set(offerings.map(o => o.courseId))];
        setCourseCount(courseIds.length);

        const courseResults = await Promise.allSettled(
          courseIds.map(id => courseService.fetchCourse(id))
        );
        let credits = 0;
        courseResults.forEach(r => {
          if (r.status === "fulfilled" && r.value?.creditHours) {
            credits += r.value.creditHours;
          }
        });
        if (!cancelled) {
          setTotalCredits(credits);
          setAcademicLoading(false);
        }
      } catch {
        if (!cancelled) setAcademicLoading(false);
      }
    };

    fetchAcademicData();
    return () => { cancelled = true; };
  }, [activeScope]);

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

  const requestStatCards = [
    { label: t("available_services"), value: services.length, icon: Package, color: "blue" },
    { label: t("active_requests"), value: requestStats?.activeRequests ?? 0, icon: ClipboardList, color: "navy" },
    { label: t("pending_requests"), value: requestStats?.pendingRequests ?? 0, icon: Clock, color: "orange" },
    { label: t("completed_requests"), value: requestStats?.completedRequests ?? 0, icon: CheckCircle, color: "green" },
  ];

  return (
    <div className="student-dashboard">
      <div className="sd-header">
        <div className="sd-welcome">
          <h1>{t("welcome")}, {user?.name || t("student")}</h1>
          <p className="sd-subtitle">{t("academic_overview_subtitle")}</p>
        </div>
      </div>

      {error && (
        <div className="alert alert-warning">
          <AlertCircle size={18} />
          <span>{error}</span>
        </div>
      )}

      {/* Academic Stats */}
      <div className="sd-stats-grid">
        <div className="sd-stat-card">
          <div className="stat-icon courses">
            <BookOpen size={24} />
          </div>
          <div className="stat-content">
            <div className="stat-value">
              {academicLoading ? <span className="sd-inline-spinner" /> : (offeringCount ?? 0)}
            </div>
            <div className="stat-label">Course Offers</div>
          </div>
        </div>

        <div className="sd-stat-card">
          <div className="stat-icon gpa">
            <BarChart3 size={24} />
          </div>
          <div className="stat-content">
            <div className="stat-value">
              {academicLoading ? <span className="sd-inline-spinner" /> : (courseCount ?? 0)}
            </div>
            <div className="stat-label">Unique Courses</div>
          </div>
        </div>

        <div className="sd-stat-card">
          <div className="stat-icon credits">
            <FileText size={24} />
          </div>
          <div className="stat-content">
            <div className="stat-value">
              {academicLoading ? <span className="sd-inline-spinner" /> : (totalCredits ?? 0)}
            </div>
            <div className="stat-label">Credit Hours</div>
          </div>
        </div>

        <div className="sd-stat-card">
          <div className="stat-icon schedule">
            <Calendar size={24} />
          </div>
          <div className="stat-content">
            <div className="stat-value">
              {academicLoading ? <span className="sd-inline-spinner" /> : (semesterName || "—")}
            </div>
            <div className="stat-label">{t("current_semester")}</div>
          </div>
        </div>
      </div>

      {/* Request Stats */}
      <div className="sd-request-stats-grid">
        {requestStatCards.map((stat, idx) => (
          <div key={idx} className={`sd-request-stat-card ${stat.color}`}>
            <div className="sd-request-stat-icon"><stat.icon size={22} /></div>
            <div className="sd-request-stat-content">
              <span>{stat.label}</span>
              <h3>{stat.value}</h3>
            </div>
          </div>
        ))}
      </div>

      {/* Quick Actions */}
      <div className="sd-section">
        <h2>{t("quick_actions")}</h2>
        <div className="sd-actions-grid">
          <Link to="/student/courses" className="action-card">
            <BookOpen size={20} />
            <div>
              <h3>{t("my_courses")}</h3>
              <p>{t("view_enrolled_courses")}</p>
            </div>
          </Link>
          <Link to="/student/courses/register" className="action-card">
            <Plus size={20} />
            <div>
              <h3>{t("register_courses")}</h3>
              <p>{t("add_new_courses")}</p>
            </div>
          </Link>
          <Link to="/student/grades" className="action-card">
            <BarChart3 size={20} />
            <div>
              <h3>{t("view_grades")}</h3>
              <p>{t("check_your_performance")}</p>
            </div>
          </Link>
          <Link to="/student/schedule" className="action-card">
            <Calendar size={20} />
            <div>
              <h3>{t("my_schedule")}</h3>
              <p>{t("class_timetable")}</p>
            </div>
          </Link>
          <Link to="/student/payments" className="action-card">
            <Receipt size={20} />
            <div>
              <h3>{t("payments_and_fees")}</h3>
              <p>{t("view_financial_status")}</p>
            </div>
          </Link>
          <Link to="/student/requests" className="action-card">
            <ClipboardList size={20} />
            <div>
              <h3>{t("my_requests")}</h3>
              <p>{t("view_my_service_requests")}</p>
            </div>
          </Link>
        </div>
      </div>

      {/* Services Catalog */}
      <div className="sd-section">
        <div className="sd-services-header">
          <h2>{t("services_catalog")}</h2>
          <Link to="/student/requests" className="sd-view-link">
            <Eye size={16} /> {t("my_requests")}
          </Link>
        </div>
        {loadingServices ? (
          <LoadingSpinner />
        ) : services.length === 0 ? (
          <div className="sd-empty">{t("no_services_available")}</div>
        ) : (
          <div className="sd-services-grid">
            {services.map(service => (
              <ServiceCard key={service.id} service={service} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

export default StudentDashboard;
