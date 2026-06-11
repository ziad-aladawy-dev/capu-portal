import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  BookOpen, Layers, Clock, MapPin, ChevronDown, ChevronRight, Send, Ban,
  CalendarDays, AlertTriangle, RefreshCw,
} from "lucide-react";
import StatusBadge from "../../../../core/components/StatusBadge";
import CapacityBar from "../../../../core/components/CapacityBar";
import ConfirmDialog from "../../../../core/components/ConfirmDialog";
import PermissionGate from "../../../../core/auth/PermissionGate";
import { useToast } from "../../../../core/components/Toast";
import { SkeletonRow } from "../../../../core/components/Skeleton";
import {
  useOfferingsForCourse, useBulkPublishOfferings, useBulkCancelOfferings,
} from "../../../../core/query/useCourseOfferings";
import { useScheduleSlots } from "../../../../core/query/useScheduleSlots";
import {
  OFFERING_STATUS_LABELS, REGISTRATION_STATE_LABELS,
} from "../../../../core/services/courseOfferingService";
import PrerequisitesPicker from "./PrerequisitesPicker";
import shared from "../../styles/academic.module.css";
import styles from "./CourseCatalog.module.css";

const statusVariant = (s) => ({ 0: "draft", 1: "open", 2: "closed", 3: "cancelled" }[s] || "inactive");
const regVariant = (s) => ({ 0: "closed", 1: "open", 2: "warning" }[s] || "inactive");
const fmtTime = (v) => (v ? String(v).slice(0, 5) : "—");
const fmtDate = (v) => (v ? new Date(v).toLocaleDateString() : "—");

function OfferingSlots({ offeringId }) {
  const { t } = useTranslation("academic");
  const { data: slots = [], isLoading } = useScheduleSlots(offeringId);
  if (isLoading) return <div style={{ padding: "8px 0" }}><SkeletonRow cols={3} /></div>;
  if (slots.length === 0) return <div className={shared.emptyInline}>{t("schedule.noSlots")}</div>;
  return (
    <div>
      {slots
        .slice()
        .sort((a, b) => a.dayOfWeek - b.dayOfWeek || String(a.startTime).localeCompare(String(b.startTime)))
        .map((s) => (
          <div key={s.id} className={styles.slotLine}>
            <span className={styles.slotDay}>{t(`schedule.days.${s.dayOfWeek}`).slice(0, 3)}</span>
            <Clock size={12} style={{ color: "#9ca3af", flexShrink: 0 }} />
            <span style={{ fontVariantNumeric: "tabular-nums" }}>{fmtTime(s.startTime)}–{fmtTime(s.endTime)}</span>
            <span className={shared.metaChip}>{t(`schedule.kinds.${s.kind}`)}</span>
            {s.location && (
              <span style={{ display: "inline-flex", alignItems: "center", gap: 3, color: "#6b7280" }}>
                <MapPin size={11} /> {s.location}
              </span>
            )}
          </div>
        ))}
    </div>
  );
}

function OfferingsTab({ course, semester }) {
  const { t } = useTranslation("academic");
  const { addToast } = useToast();
  const [expanded, setExpanded] = useState(null);
  const [selected, setSelected] = useState(new Set());
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState("");

  const { data: offerings = [], isLoading, error, refetch } = useOfferingsForCourse(course?.id, semester?.id);
  const bulkPublish = useBulkPublishOfferings();
  const bulkCancel = useBulkCancelOfferings();

  const stats = useMemo(() => {
    const totalSeats = offerings.reduce((a, o) => a + (o.capacity || 0), 0);
    const filledSeats = offerings.reduce((a, o) => a + (o.registeredCount || 0), 0);
    const full = offerings.filter((o) => o.capacity > 0 && o.registeredCount >= o.capacity).length;
    return { count: offerings.length, totalSeats, filledSeats, full };
  }, [offerings]);

  const toggleSelect = (id) =>
    setSelected((prev) => {
      const n = new Set(prev);
      if (n.has(id)) n.delete(id); else n.add(id);
      return n;
    });

  const handlePublish = async () => {
    const ids = [...selected];
    if (!ids.length) return;
    try {
      await bulkPublish.mutateAsync(ids);
      addToast(t("offerings.published", { count: ids.length }), "success");
      setSelected(new Set());
    } catch (e) {
      addToast(e.message || t("common.retry"), "error");
    }
  };

  const handleCancel = async () => {
    const ids = [...selected];
    if (!ids.length || !cancelReason.trim()) return;
    try {
      await bulkCancel.mutateAsync({ ids, reason: cancelReason });
      addToast(t("offerings.cancelledToast", { count: ids.length }), "success");
      setSelected(new Set());
      setCancelReason("");
      setCancelOpen(false);
    } catch (e) {
      addToast(e.message || t("common.retry"), "error");
    }
  };

  return (
    <div>
      <div style={{ display: "flex", gap: 16, marginBottom: 12, flexWrap: "wrap" }}>
        <div><div className={shared.statValue}>{stats.count}</div><div className={shared.statLabel}>{t("courses.offeringsCount")}</div></div>
        <div><div className={shared.statValue}>{stats.filledSeats}/{stats.totalSeats}</div><div className={shared.statLabel}>{t("offerings.statsSeats")}</div></div>
        <div><div className={shared.statValue} style={{ color: stats.full ? "#b91c1c" : undefined }}>{stats.full}</div><div className={shared.statLabel}>{t("offerings.statsFull")}</div></div>
      </div>

      {!semester && <div className={shared.emptyInline}>{t("common.noSemester")}</div>}

      {error && (
        <div className={shared.errorBanner} role="alert">
          <AlertTriangle size={14} /> {error.message}
          <button className="btn-cancel" style={{ marginLeft: "auto", padding: "3px 8px", fontSize: 11 }} onClick={() => refetch()}>
            <RefreshCw size={11} /> {t("common.retry")}
          </button>
        </div>
      )}

      {isLoading && semester && (
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          <SkeletonRow cols={4} /><SkeletonRow cols={4} />
        </div>
      )}

      {!isLoading && semester && offerings.length === 0 && !error && (
        <div className={shared.emptyInline}>{t("offerings.noOfferings")}</div>
      )}

      {offerings.map((o) => {
        const isOpen = expanded === o.id;
        const isSel = selected.has(o.id);
        return (
          <div key={o.id} className={`${styles.offeringCard}${isSel ? ` ${styles.offeringSelected}` : ""}`}>
            <div className={styles.offeringRow}>
              <input
                type="checkbox"
                checked={isSel}
                onChange={() => toggleSelect(o.id)}
                aria-label={`${t("offerings.section")} ${o.sectionCode}`}
              />
              <div className={styles.offeringMain} onClick={() => setExpanded(isOpen ? null : o.id)}>
                <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                  <span className={styles.offeringSection}>{t("offerings.section")} {o.sectionCode}</span>
                  <StatusBadge status={statusVariant(o.status)} label={OFFERING_STATUS_LABELS[o.status]} />
                  <StatusBadge status={regVariant(o.registrationState)} label={REGISTRATION_STATE_LABELS[o.registrationState]} />
                </div>
              </div>
              <CapacityBar registered={o.registeredCount} capacity={o.capacity} width={110} />
              <button
                className="btn-cancel"
                style={{ padding: "4px 6px" }}
                onClick={() => setExpanded(isOpen ? null : o.id)}
                aria-label={isOpen ? "Collapse" : "Expand"}
              >
                {isOpen ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
              </button>
            </div>
            {isOpen && <div className={styles.offeringSlots}><OfferingSlots offeringId={o.id} /></div>}
          </div>
        );
      })}

      {selected.size > 0 && (
        <PermissionGate resource="course-offerings.course-offerings" minLevel={3}>
          <div style={{ display: "flex", gap: 8, marginTop: 12, padding: 10, background: "#f8f9fc", borderRadius: 8, alignItems: "center" }}>
            <span style={{ fontSize: 12, color: "#6b7280" }}>{t("common.selected", { count: selected.size })}</span>
            <button className="btn-primary" style={{ marginLeft: "auto" }} onClick={handlePublish} disabled={bulkPublish.isPending}>
              <Send size={13} /> {t("offerings.publish")}
            </button>
            <button className="btn-cancel" onClick={() => setCancelOpen(true)} disabled={bulkCancel.isPending}>
              <Ban size={13} /> {t("common.cancel")}
            </button>
          </div>
        </PermissionGate>
      )}

      <ConfirmDialog
        open={cancelOpen}
        onClose={() => { setCancelOpen(false); setCancelReason(""); }}
        onConfirm={handleCancel}
        title={t("offerings.cancelOfferings")}
        message={t("offerings.cancelMsg", { count: selected.size })}
        detail={t("offerings.cancelDetail")}
        confirmLabel={t("common.confirm")}
        variant="danger"
        loading={bulkCancel.isPending}
      >
        <div style={{ marginTop: 12 }}>
          <input
            type="text"
            className={shared.formInput}
            style={{ width: "100%", boxSizing: "border-box" }}
            placeholder={t("offerings.cancelReasonPlaceholder")}
            value={cancelReason}
            onChange={(e) => setCancelReason(e.target.value)}
            autoFocus
          />
        </div>
      </ConfirmDialog>
    </div>
  );
}

/**
 * Right pane of the catalog split view. Tabs: details / prerequisites /
 * offerings (per the navbar semester).
 */
export default function CourseDetailPanel({ course, semester, categoryLabel, canEditPrereqs }) {
  const { t } = useTranslation("academic");
  const [tab, setTab] = useState("details");

  if (!course) {
    return (
      <div className={styles.panelEmpty}>
        <BookOpen size={28} strokeWidth={1.4} />
        <strong>{t("courses.selectCourse")}</strong>
        <p>{t("courses.selectCourseHint")}</p>
      </div>
    );
  }

  return (
    <div className={styles.panel}>
      <div className={styles.panelHead}>
        <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
          <span className={shared.codePill}>{course.code}</span>
          <strong style={{ fontSize: 14.5 }}>{course.title}</strong>
        </div>
        <div className={styles.panelMeta}>
          <span className={shared.metaChip}>{course.creditHours} {t("common.credits")}</span>
          <span className={shared.metaChip}>{categoryLabel}</span>
          <StatusBadge status={course.isActive ? "active" : "inactive"} label={course.isActive ? t("common.active") : t("common.inactive")} />
          {course.isClosed && <StatusBadge status="closed" label={t("courses.recordClosed")} />}
        </div>
      </div>

      <div className={styles.panelTabs} role="tablist">
        {[
          { id: "details", label: t("courses.detailsTab") },
          { id: "prereqs", label: t("courses.prerequisitesTab") },
          { id: "offerings", label: t("courses.offeringsTab") },
        ].map((x) => (
          <button
            key={x.id}
            role="tab"
            aria-selected={tab === x.id}
            className={tab === x.id ? styles.panelTabActive : styles.panelTab}
            onClick={() => setTab(x.id)}
          >
            {x.label}
          </button>
        ))}
      </div>

      {tab === "details" && (
        <dl className={styles.detailGrid}>
          <dt>{t("courses.code")}</dt><dd>{course.code}</dd>
          <dt>{t("courses.courseTitle")}</dt><dd>{course.title}</dd>
          <dt>{t("courses.creditHours")}</dt><dd>{course.creditHours}</dd>
          <dt>{t("courses.category")}</dt><dd>{categoryLabel}</dd>
          <dt>{t("common.status")}</dt><dd>{course.isActive ? t("common.active") : t("common.inactive")}</dd>
          <dt>{t("common.record")}</dt><dd>{course.isClosed ? t("common.closed") : t("common.open")}</dd>
          {course.createdAt && (<><dt>{t("courses.createdAt")}</dt><dd>{fmtDate(course.createdAt)}</dd></>)}
          {course.updatedAt && (<><dt>{t("courses.updatedAt")}</dt><dd>{fmtDate(course.updatedAt)}</dd></>)}
          <dt style={{ display: "flex", alignItems: "center", gap: 4 }}><CalendarDays size={12} /> {t("common.semester")}</dt>
          <dd>{semester?.name || t("common.none")}</dd>
        </dl>
      )}

      {tab === "prereqs" && (
        <PrerequisitesPicker course={course} readOnly={!canEditPrereqs || course.isClosed} />
      )}

      {tab === "offerings" && (
        <div>
          <div className={shared.sectionTitle}>
            <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
              <Layers size={14} /> {t("courses.offeringsTab")} — {semester?.name || t("common.none")}
            </span>
          </div>
          <OfferingsTab course={course} semester={semester} />
        </div>
      )}
    </div>
  );
}
