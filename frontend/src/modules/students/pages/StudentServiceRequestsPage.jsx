import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ClipboardList, ArrowLeft, ExternalLink, Clock, CheckCircle, XCircle, AlertCircle } from "lucide-react";
import { useStudentServiceRequests } from "../../../core/query/useStudentServiceRequests";
import { Panel, EmptyState } from "../../users/components/ProfileKit";
import ErrorMessage from "../../../core/components/ErrorMessage";
import { SkeletonCard } from "../../../core/components/Skeleton";

const REQUEST_STATUS_LABEL = {
  0: "Pending",
  1: "In Progress",
  2: "Completed",
  3: "Rejected",
  4: "Cancelled",
};

function StudentServiceRequestsPage() {
  const { id } = useParams();
  return <StudentServiceRequestsContent key={id} studentId={id} />;
}

function StudentServiceRequestsContent({ studentId }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data: requests, isLoading, error } = useStudentServiceRequests(studentId);

  if (isLoading) {
    return (
      <div className="pp-page">
        <SkeletonCard height={40} />
        <div style={{ height: 14 }} />
        <SkeletonCard height={200} />
      </div>
    );
  }
  if (error) {
    return <ErrorMessage message={error?.message || t("failed_to_load_requests")} />;
  }

  return (
    <div className="pp-page">
      <div className="pp-topbar">
        <button className="pp-back" onClick={() => navigate(`/admin/students/${studentId}`)}>
          <ArrowLeft size={13} /> {t("back_to_profile")}
        </button>
      </div>

      <Panel icon={ClipboardList} title={t("service_requests")}>
        {!requests || requests.length === 0 ? (
          <EmptyState
            icon={ClipboardList}
            title={t("no_service_requests")}
            message={t("no_service_requests_desc")}
          />
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table className="pp-table">
              <thead>
                <tr>
                  <th>{t("service")}</th>
                  <th>{t("status")}</th>
                  <th>{t("submitted")}</th>
                  <th>{t("actions")}</th>
                </tr>
              </thead>
              <tbody>
                {requests.map((req) => (
                  <tr key={req.id}>
                    <td>{req.serviceName || req.service?.name || "—"}</td>
                    <td>
                      <span className={`pp-pill ${getStatusClass(req.status)}`}>
                        {REQUEST_STATUS_LABEL[req.status] || req.status}
                      </span>
                    </td>
                    <td>{req.createdAt ? new Date(req.createdAt).toLocaleDateString() : "—"}</td>
                    <td>
                      <button
                        className="pp-btn soft"
                        onClick={() => navigate(`/admin/student-services/requests/${req.id}`)}
                      >
                        <ExternalLink size={12} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>
    </div>
  );
}

function getStatusClass(status) {
  switch (status) {
    case 0: return "warn";
    case 1: return "info";
    case 2: return "good";
    case 3: return "bad";
    case 4: return "bad";
    default: return "";
  }
}

export default StudentServiceRequestsPage;
