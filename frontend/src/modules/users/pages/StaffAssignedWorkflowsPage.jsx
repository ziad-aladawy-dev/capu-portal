import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { GitBranch, ArrowLeft } from "lucide-react";
import { useStaffAssignedWorkflows } from "../../../core/query/useStaffAssignedWorkflows";
import { Panel, EmptyState } from "../components/ProfileKit";
import ErrorMessage from "../../../core/components/ErrorMessage";
import { SkeletonCard } from "../../../core/components/Skeleton";
import AssignedWorkflowSteps from "../components/AssignedWorkflowSteps";

function StaffAssignedWorkflowsPage() {
  const { id } = useParams();
  return <StaffAssignedWorkflowsContent key={id} staffId={id} />;
}

function StaffAssignedWorkflowsContent({ staffId }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data: workflows, isLoading, error } = useStaffAssignedWorkflows(staffId);

  const handleViewRequest = (item) => {
    navigate(`/admin/student-services/requests/${item.id}`);
  };

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
    return <ErrorMessage message={error?.message || t("failed_to_load_workflows")} />;
  }

  return (
    <div className="pp-page">
      <div className="pp-topbar">
        <button className="pp-back" onClick={() => navigate(`/admin/users/${staffId}`)}>
          <ArrowLeft size={13} /> {t("back_to_profile")}
        </button>
      </div>

      <Panel icon={GitBranch} title={t("assigned_workflows")}>
        {!workflows || workflows.length === 0 ? (
          <EmptyState
            icon={GitBranch}
            title={t("no_assigned_workflows")}
            message={t("no_assigned_workflows_desc")}
          />
        ) : (
          <div className="sawf-list">
            {workflows.map((wf) => (
              <AssignedWorkflowSteps
                key={wf.id}
                item={wf}
                onViewRequest={handleViewRequest}
              />
            ))}
          </div>
        )}
      </Panel>
    </div>
  );
}

export default StaffAssignedWorkflowsPage;
