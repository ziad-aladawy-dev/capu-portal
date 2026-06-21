import { useParams, useNavigate, Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { CreditCard, FileText, AlertCircle, ClipboardList } from "lucide-react";
import { getAvailableServicesForStudent } from "../../studentServices/services/studentServicesService";
import { usePendingRequestForService } from "../hooks/useStudentRequests";
import { WORKFLOW_STEP_TYPE_NAMES } from "../constants/workflowTypes";
import { SERVICE_TYPE_LABELS } from "../constants/requestStatus";
import { fmtAmount } from "../../../core/services/treasuryPaymentService";
import { getLocalized } from "../../../core/utils/getLocalized";
import PortalPageShell from "../components/shared/PortalPageShell";
import PortalCard from "../components/shared/PortalCard";
import PortalBadge from "../components/shared/PortalBadge";
import PortalSkeleton from "../components/shared/PortalSkeleton";
import PortalEmptyState from "../components/shared/PortalEmptyState";
import { useAuth } from "../../../core/auth/useAuth";
import styles from "./StudentServiceDetails.module.css";

function StudentServiceDetails() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const { user } = useAuth();

  const serviceQuery = useQuery({
    queryKey: ["portal", "services", "detail", String(id)],
    enabled: Boolean(id) && Boolean(user?.id),
    queryFn: async () => {
      const services = await getAvailableServicesForStudent(user.id);
      const service = services.find(s => s.id === id);
      if (!service) {
        throw new Error("Service not found or not available for you");
      }
      return service;
    },
  });
  const service = serviceQuery.data;

  const pendingQuery = usePendingRequestForService(id);
  const pending = pendingQuery.data;

  const localizedName = service ? getLocalized(service.name, i18n.language) || service.name : "";
  const localizedDesc = service ? getLocalized(service.description, i18n.language) || service.description : "";

  const shellProps = {
    title: localizedName || t("portal_services.details_title", { defaultValue: "Service details" }),
    backTo: "/student/services",
  };

  if (serviceQuery.isLoading) {
    return (
      <PortalPageShell {...shellProps}>
        <div className={styles.loading}>
          <PortalSkeleton variant="block" height={90} />
          <PortalSkeleton variant="block" height={180} />
        </div>
      </PortalPageShell>
    );
  }

  if (serviceQuery.isError || !service) {
    return (
      <PortalPageShell {...shellProps}>
        <PortalEmptyState
          icon={AlertCircle}
          title={t("portal_services.not_found", { defaultValue: "Service not found." })}
          onAction={() => serviceQuery.refetch()}
          actionLabel={t("retry", { defaultValue: "Retry" })}
        />
      </PortalPageShell>
    );
  }

  const workflowSteps = [...(service.workflow?.steps || [])].sort(
    (a, b) => (a.order ?? 0) - (b.order ?? 0)
  );

  return (
    <PortalPageShell
      {...shellProps}
      subtitle={SERVICE_TYPE_LABELS[service.type]
        ? t(`portal_services.type_${service.type}`, { defaultValue: SERVICE_TYPE_LABELS[service.type] })
        : undefined}
      actions={
        <PortalBadge tone={service.isActive ? "success" : "neutral"}>
          {service.isActive
            ? t("portal_services.active", { defaultValue: "Active" })
            : t("portal_services.inactive", { defaultValue: "Unavailable" })}
        </PortalBadge>
      }
    >
      <div className={styles.grid}>
        <div className={styles.main}>
          <PortalCard className={styles.card}>
            <h3 className={styles.cardTitle}>
              <FileText size={14} /> {t("portal_services.description", { defaultValue: "Description" })}
            </h3>
            <p className={styles.cardText}>
              {localizedDesc || t("portal_services.no_description", { defaultValue: "No description provided." })}
            </p>
          </PortalCard>

          <PortalCard className={styles.card}>
            <h3 className={styles.cardTitle}>
              <ClipboardList size={14} /> {t("portal_services.workflow_preview", { defaultValue: "Application Steps" })}
            </h3>
            {workflowSteps.length === 0 ? (
              <p className={styles.cardText}>
                {t("portal_services.no_workflow", { defaultValue: "No application steps defined for this service yet." })}
              </p>
            ) : (
              <ol className={styles.stepList}>
                {workflowSteps.map((step, idx) => {
                  const stepTitle = getLocalized(step.title, i18n.language) || step.title;
                  const stepDesc = getLocalized(step.description, i18n.language) || step.description;
                  return (
                    <li key={`${step.order}-${idx}`} className={styles.stepItem}>
                      <span className={styles.stepNum}>{idx + 1}</span>
                      <div className={styles.stepBody}>
                        <span className={styles.stepTitle}>{stepTitle}</span>
                        <span className={styles.stepType}>
                          {t(`portal_services.step_type_${WORKFLOW_STEP_TYPE_NAMES[step.stepType] || "Form"}`, {
                            defaultValue: WORKFLOW_STEP_TYPE_NAMES[step.stepType] || "",
                          })}
                        </span>
                        {stepDesc && <p className={styles.stepDesc}>{stepDesc}</p>}
                      </div>
                    </li>
                  );
                })}
              </ol>
            )}
          </PortalCard>
        </div>

        <div className={styles.side}>
          <PortalCard className={styles.feeCard}>
            <span className={styles.feeLabel}>
              <CreditCard size={15} /> {t("portal_services.price", { defaultValue: "Service fee" })}
            </span>
            <strong className={styles.feeValue}>
              {service.isPaid
                ? `${fmtAmount(service.price)} EGP`
                : t("portal_services.free", { defaultValue: "Free" })}
            </strong>
          </PortalCard>

          {pending?.id ? (
            <>
              <p className={styles.pendingNote}>
                {t("portal_services.existing_request", { defaultValue: "You already have an open request for this service." })}
              </p>
              <Link to={`/student/requests/${pending.id}`} className={styles.applyBtn}>
                {t("portal_services.view_existing_request", { defaultValue: "View existing request" })}
              </Link>
            </>
          ) : (
            <button
              type="button"
              className={styles.applyBtn}
              onClick={() => navigate(`/student/services/${id}/apply`)}
              disabled={!service.isActive || pendingQuery.isLoading}
            >
              {service.isActive
                ? t("portal_services.apply", { defaultValue: "Apply for this service" })
                : t("portal_services.service_closed", { defaultValue: "Service unavailable" })}
            </button>
          )}
        </div>
      </div>
    </PortalPageShell>
  );
}

export default StudentServiceDetails;