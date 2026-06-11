import { useState, useMemo, useRef } from "react";
import { useTranslation } from "react-i18next";
import {
  CalendarRange, AlertTriangle, MapPin, Users, Layers, RefreshCw, Trash2,
} from "lucide-react";
import { getLocalized } from "../../../../core/utils/getLocalized";
import { useDomain } from "../../../../core/contexts/DomainContext";
import { useAcademic } from "../../../../core/contexts/AcademicContext";
import { useToast } from "../../../../core/components/Toast";
import Drawer from "../../../../core/components/Drawer";
import EmptyState from "../../../../core/components/EmptyState";
import StatusBadge from "../../../../core/components/StatusBadge";
import ConfirmDialog from "../../../../core/components/ConfirmDialog";
import PermissionGate from "../../../../core/auth/PermissionGate";
import { SkeletonCard } from "../../../../core/components/Skeleton";
import ScheduleGrid from "../../../../core/components/ScheduleGrid";
import { findConflicts } from "../../../../core/utils/scheduleConflicts";
import { SCHEDULE_START_HOUR, SCHEDULE_END_HOUR } from "../../../../core/constants/scheduleConfig";
import {
  useOfferingsForSchedule, useSlotsForOfferings, useUpdateSlot, useDeleteSlot,
} from "../../../../core/query/useScheduleSlots";
import { useActiveCourses } from "../../../../core/query/useCourses";
import ScheduleSlotForm from "./ScheduleSlotForm";
import "./schedulingMatrix.css";

const KIND_LEGEND = [
  { kind: 0, color: "#c7d2fe" },
  { kind: 1, color: "#a7f3d0" },
  { kind: 2, color: "#fde68a" },
  { kind: 3, color: "#ddd6fe" },
  { kind: 4, color: "#fecdd3" },
];
const fmtTime = (t) => (t ? String(t).slice(0, 5) : "");

export default function SchedulingMatrixPage() {
  const { t, i18n } = useTranslation("academic");
  const { scopeNode } = useDomain();
  const { selectedSemesterObj, selectedSemester } = useAcademic();
  const { addToast } = useToast();
  const scopeName = getLocalized(scopeNode?.name, i18n.language);

  const { data: offerings = [], isLoading: offeringsLoading, error, refetch } =
    useOfferingsForSchedule(scopeNode?.id, selectedSemesterObj?.id);

  const offeringIds = useMemo(() => offerings.map((o) => o.id), [offerings]);
  const { slotsByOffering, isLoading: slotsLoading } = useSlotsForOfferings(offeringIds);
  const { data: courses = [] } = useActiveCourses();

  const updateSlot = useUpdateSlot();
  const deleteSlot = useDeleteSlot();

  const [kindFilter, setKindFilter] = useState("");
  const [editSlot, setEditSlot] = useState(null);
  const [drawerError, setDrawerError] = useState("");
  const [confirmDelete, setConfirmDelete] = useState(null);
  const submitRef = useRef(null);

  const courseById = useMemo(() => {
    const m = {};
    courses.forEach((c) => (m[c.id] = c));
    return m;
  }, [courses]);

  // Flatten all slots, enriched with offering + course context.
  const enrichedSlots = useMemo(() => {
    const out = [];
    for (const o of offerings) {
      const course = courseById[o.courseId];
      for (const s of slotsByOffering[o.id] || []) {
        if (kindFilter !== "" && s.kind !== Number(kindFilter)) continue;
        out.push({
          ...s,
          _offering: o,
          _courseCode: course?.code || "—",
          _courseTitle: course?.title || "",
        });
      }
    }
    return out;
  }, [offerings, slotsByOffering, courseById, kindFilter]);

  const conflicts = useMemo(() => findConflicts(enrichedSlots), [enrichedSlots]);
  const conflictPairs = conflicts.size;

  const handleSave = async (formData) => {
    if (!editSlot) return;
    setDrawerError("");
    try {
      await updateSlot.mutateAsync({
        id: editSlot.id,
        dayOfWeek: formData.dayOfWeek,
        startTime: `${formData.startTime}:00`,
        endTime: `${formData.endTime}:00`,
        kind: formData.kind,
        location: formData.location || null,
        notes: formData.notes || null,
      });
      addToast(t("schedule.slotUpdated"), "success");
      setEditSlot(null);
    } catch (e) {
      setDrawerError(e.message || t("courses.saveFailed"));
    }
  };

  const handleDelete = async () => {
    if (!confirmDelete) return;
    try {
      await deleteSlot.mutateAsync(confirmDelete.id);
      addToast(t("schedule.slotDeleted"), "success");
      setConfirmDelete(null);
      setEditSlot(null);
    } catch (e) {
      addToast(e.message || t("courses.saveFailed"), "error");
      setConfirmDelete(null);
    }
  };

  const renderSlot = (slot, { conflict }) => {
    const o = slot._offering;
    return (
      <div
        className={`sm-block kind-${slot.kind}${conflict ? " conflict" : ""}`}
        title={`${slot._courseCode} ${slot._courseTitle} — ${t("offerings.section")} ${o?.sectionCode}`}
      >
        <span className="sm-block-code">
          {slot._courseCode} · {o?.sectionCode}
        </span>
        <span className="sm-block-meta" style={{ fontVariantNumeric: "tabular-nums" }}>
          {fmtTime(slot.startTime)}–{fmtTime(slot.endTime)}
        </span>
        {slot.location && (
          <span className="sm-block-meta"><MapPin size={10} /> {slot.location}</span>
        )}
        <span className="sm-block-meta">
          <Users size={10} /> {o?.registeredCount}/{o?.capacity}
          {conflict && <AlertTriangle size={11} style={{ color: "#ef4444", marginLeft: 2 }} />}
        </span>
      </div>
    );
  };

  if (!scopeNode || !selectedSemesterObj) {
    return (
      <>
        <div className="sm-page" style={{ padding: 0 }}>
          <div className="sm-header">
            <div className="sm-header-left">
              <CalendarRange size={20} />
              <div>
                <h1>{t("schedule.matrixTitle")}</h1>
                <p>{t("schedule.matrixSubtitle")}</p>
              </div>
            </div>
          </div>
          <EmptyState
            icon={CalendarRange}
            title={t("common.semester")}
            message={t("common.noSemester")}
          />
        </div>
      </>
    );
  }

  const isLoading = offeringsLoading || slotsLoading;

  return (
    <>
    <div className="sm-page" style={{ padding: 0 }}>
      <div className="sm-header">
        <div className="sm-header-left">
          <CalendarRange size={20} />
          <div>
            <h1>{t("schedule.matrixTitle")}</h1>
            <p>{scopeName} — {selectedSemester} · {offerings.length} / {enrichedSlots.length}</p>
          </div>
        </div>
      </div>

      <div className="sm-toolbar">
        <select
          value={kindFilter}
          onChange={(e) => setKindFilter(e.target.value)}
          aria-label={t("schedule.kind")}
          style={{ border: "1px solid #e5e7eb", background: "white", borderRadius: 8, padding: "8px 12px", fontSize: 13, fontFamily: "inherit" }}
        >
          <option value="">{t("schedule.allKinds")}</option>
          {[0, 1, 2, 3, 4, 5].map((k) => <option key={k} value={k}>{t(`schedule.kinds.${k}`)}</option>)}
        </select>
        <button className="btn-cancel" onClick={() => refetch()}><RefreshCw size={13} /> {t("common.retry")}</button>
        <div className="sm-legend" aria-label={t("schedule.legend")}>
          {KIND_LEGEND.map((k) => (
            <span key={k.kind} className="sm-legend-item">
              <span className="sm-swatch" style={{ background: k.color }} /> {t(`schedule.kinds.${k.kind}`)}
            </span>
          ))}
          <span className="sm-legend-item">
            <span className="sm-swatch" style={{ background: "#fef2f2", border: "2px solid #ef4444" }} /> ⚠
          </span>
        </div>
      </div>

      {conflictPairs > 0 && (
        <div className="sm-conflict-banner" role="status">
          <AlertTriangle size={16} />
          {t("schedule.conflicts", { count: conflictPairs })}
        </div>
      )}

      {error && (
        <div className="sm-conflict-banner" role="alert">
          <AlertTriangle size={16} /> {error.message}
          <button className="btn-cancel" style={{ marginLeft: "auto", padding: "4px 10px" }} onClick={() => refetch()}>{t("common.retry")}</button>
        </div>
      )}

      {isLoading ? (
        <SkeletonCard height={420} />
      ) : enrichedSlots.length === 0 ? (
        <div className="sm-empty">
          <Layers size={40} style={{ color: "#c9a84c" }} />
          <h3 style={{ color: "#1a1f5e", margin: 0, fontSize: 15 }}>{t("schedule.noSlots")}</h3>
          <p style={{ margin: 0, fontSize: 13 }}>
            {offerings.length === 0 ? t("offerings.noOfferingsHint") : t("schedule.noSlots")}
          </p>
        </div>
      ) : (
        <ScheduleGrid
          slots={enrichedSlots}
          renderSlot={renderSlot}
          onSlotClick={(s) => { setEditSlot(s); setDrawerError(""); }}
          startHour={SCHEDULE_START_HOUR}
          endHour={SCHEDULE_END_HOUR}
        />
      )}

      {/* Edit slot drawer — shared ScheduleSlotForm */}
      <Drawer
        open={!!editSlot}
        onClose={() => setEditSlot(null)}
        title={t("schedule.editSlot")}
        width={440}
        loading={updateSlot.isPending}
        footer={
          <>
            <PermissionGate resource="schedule.schedule-slots" minLevel={5}>
              <button className="btn-cancel" style={{ color: "#b91c1c", marginRight: "auto" }} onClick={() => setConfirmDelete(editSlot)}>
                <Trash2 size={13} /> {t("common.delete")}
              </button>
            </PermissionGate>
            <button className="btn-cancel" onClick={() => setEditSlot(null)}>{t("common.cancel")}</button>
            <button className="btn-primary" onClick={() => submitRef.current?.()} disabled={updateSlot.isPending}>
              {updateSlot.isPending ? t("common.saving") : t("common.save")}
            </button>
          </>
        }
      >
        {editSlot && (
          <>
            <div style={{ padding: 12, background: "#f8f9fc", borderRadius: 8, marginBottom: 14 }}>
              <div style={{ fontWeight: 700, fontSize: 14 }}>{editSlot._courseCode} · {t("offerings.section")} {editSlot._offering?.sectionCode}</div>
              <div style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>{editSlot._courseTitle}</div>
              {conflicts.has(editSlot.id) && (
                <div style={{ marginTop: 6 }}><StatusBadge status="cancelled" label={t("schedule.conflicts", { count: 1 })} /></div>
              )}
            </div>
            <ScheduleSlotForm
              key={editSlot?.id}
              slot={editSlot}
              onSubmit={handleSave}
              submitRef={submitRef}
              extraError={drawerError}
            />
          </>
        )}
      </Drawer>

      <ConfirmDialog
        open={!!confirmDelete}
        onClose={() => setConfirmDelete(null)}
        onConfirm={handleDelete}
        title={t("schedule.deleteSlot")}
        message={t("schedule.deleteSlotMsg", {
          kind: confirmDelete ? t(`schedule.kinds.${confirmDelete.kind}`) : "",
          day: confirmDelete ? t(`schedule.days.${confirmDelete.dayOfWeek}`) : "",
        })}
        confirmLabel={t("common.delete")}
        variant="danger"
        loading={deleteSlot.isPending}
      />
    </div>
    </>
  );
}
