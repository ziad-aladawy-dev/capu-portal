import { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  CalendarRange, AlertTriangle, MapPin, Users, Layers, RefreshCw, Trash2,
} from "lucide-react";
import { getLocalized } from "../../../core/utils/getLocalized";
import { useDomain } from "../../../core/contexts/DomainContext";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import { useToast } from "../../../core/components/Toast";
import Drawer from "../../../core/components/Drawer";
import EmptyState from "../../../core/components/EmptyState";
import StatusBadge from "../../../core/components/StatusBadge";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import PermissionGate from "../../../core/auth/PermissionGate";
import { SkeletonCard } from "../../../core/components/Skeleton";
import ScheduleGrid from "../../../core/components/ScheduleGrid";
import { findConflicts } from "../../../core/utils/scheduleConflicts";
import {
  useOfferingsForSchedule, useSlotsForOfferings, useUpdateSlot, useDeleteSlot,
} from "../../../core/query/useScheduleSlots";
import { useActiveCourses } from "../../../core/query/useCourses";
import { SLOT_KIND_LABELS, DAY_LABELS } from "../../../core/services/scheduleService";
import "../styles/schedulingMatrix.css";

const KIND_LEGEND = [
  { kind: 0, color: "#c7d2fe" },
  { kind: 1, color: "#a7f3d0" },
  { kind: 2, color: "#fde68a" },
  { kind: 3, color: "#ddd6fe" },
  { kind: 4, color: "#fecdd3" },
];
const fmtTime = (t) => (t ? String(t).slice(0, 5) : "");

export default function SchedulingMatrixPage() {
  const { i18n } = useTranslation();
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
  const [form, setForm] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(null);

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

  const openEdit = (slot) => {
    setEditSlot(slot);
    setForm({
      dayOfWeek: slot.dayOfWeek,
      startTime: fmtTime(slot.startTime),
      endTime: fmtTime(slot.endTime),
      kind: slot.kind,
      location: slot.location || "",
      notes: slot.notes || "",
    });
  };

  const handleSave = async () => {
    if (!editSlot || !form) return;
    try {
      await updateSlot.mutateAsync({
        id: editSlot.id,
        dayOfWeek: Number(form.dayOfWeek),
        startTime: form.startTime.length === 5 ? `${form.startTime}:00` : form.startTime,
        endTime: form.endTime.length === 5 ? `${form.endTime}:00` : form.endTime,
        kind: Number(form.kind),
        location: form.location || null,
        notes: form.notes || null,
      });
      addToast("Slot updated", "success");
      setEditSlot(null);
    } catch (e) {
      addToast(e.message || "Failed to update slot", "error");
    }
  };

  const handleDelete = async () => {
    if (!confirmDelete) return;
    try {
      await deleteSlot.mutateAsync(confirmDelete.id);
      addToast("Slot deleted", "success");
      setConfirmDelete(null);
      setEditSlot(null);
    } catch (e) {
      addToast(e.message || "Failed to delete", "error");
      setConfirmDelete(null);
    }
  };

  const renderSlot = (slot, { conflict }) => {
    const o = slot._offering;
    return (
      <div
        className={`sm-block kind-${slot.kind}${conflict ? " conflict" : ""}`}
        title={`${slot._courseCode} ${slot._courseTitle} — Section ${o?.sectionCode}`}
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
      <div className="sm-page">
        <div className="sm-header">
          <div className="sm-header-left">
            <CalendarRange size={20} />
            <div>
              <h1>Scheduling Matrix</h1>
              <p>Visual timetable with live conflict detection</p>
            </div>
          </div>
        </div>
        <EmptyState
          icon={CalendarRange}
          title={!scopeNode ? "Select a structure node" : "Select a semester"}
          message="Choose a structure node and semester in the context bar to load the timetable."
        />
      </div>
    );
  }

  const isLoading = offeringsLoading || slotsLoading;

  return (
    <div className="sm-page">
      <div className="sm-header">
        <div className="sm-header-left">
          <CalendarRange size={20} />
          <div>
            <h1>Scheduling Matrix</h1>
            <p>{scopeName} — {selectedSemester} · {offerings.length} offering(s), {enrichedSlots.length} slot(s)</p>
          </div>
        </div>
      </div>

      <div className="sm-toolbar">
        <select
          value={kindFilter}
          onChange={(e) => setKindFilter(e.target.value)}
          aria-label="Filter by slot kind"
          style={{ border: "1px solid #e5e7eb", background: "white", borderRadius: 8, padding: "8px 12px", fontSize: 13, fontFamily: "inherit" }}
        >
          <option value="">All kinds</option>
          {Object.entries(SLOT_KIND_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
        </select>
        <button className="btn-cancel" onClick={() => refetch()}><RefreshCw size={13} /> Refresh</button>
        <div className="sm-legend">
          {KIND_LEGEND.map((k) => (
            <span key={k.kind} className="sm-legend-item">
              <span className="sm-swatch" style={{ background: k.color }} /> {SLOT_KIND_LABELS[k.kind]}
            </span>
          ))}
          <span className="sm-legend-item">
            <span className="sm-swatch" style={{ background: "#fef2f2", border: "2px solid #ef4444" }} /> Conflict
          </span>
        </div>
      </div>

      {conflictPairs > 0 && (
        <div className="sm-conflict-banner" role="status">
          <AlertTriangle size={16} />
          <strong>{conflictPairs} slot(s)</strong> overlap in time within this node &amp; semester. Outlined in red below.
        </div>
      )}

      {error && (
        <div className="sm-conflict-banner" role="alert">
          <AlertTriangle size={16} /> Failed to load offerings.
          <button className="btn-cancel" style={{ marginLeft: "auto", padding: "4px 10px" }} onClick={() => refetch()}>Retry</button>
        </div>
      )}

      {isLoading ? (
        <SkeletonCard height={420} />
      ) : enrichedSlots.length === 0 ? (
        <div className="sm-empty">
          <Layers size={40} style={{ color: "#c9a84c" }} />
          <h3 style={{ color: "#1a1f5e", margin: 0, fontSize: 15 }}>No scheduled slots</h3>
          <p style={{ margin: 0, fontSize: 13 }}>
            {offerings.length === 0
              ? "No offerings exist for this node & semester. Offerings usually live on program-level nodes — try picking a specific program in the scope selector above."
              : "Offerings exist but none have schedule slots assigned."}
          </p>
        </div>
      ) : (
        <ScheduleGrid
          slots={enrichedSlots}
          renderSlot={renderSlot}
          onSlotClick={openEdit}
          startHour={8}
          endHour={21}
        />
      )}

      {/* Edit slot drawer */}
      <Drawer
        open={!!editSlot}
        onClose={() => setEditSlot(null)}
        title="Edit Schedule Slot"
        width={440}
        loading={updateSlot.isPending}
        footer={
          <>
            <PermissionGate resource="schedule.schedule-slots" minLevel={5}>
              <button className="btn-cancel" style={{ color: "#b91c1c", marginRight: "auto" }} onClick={() => setConfirmDelete(editSlot)}>
                <Trash2 size={13} /> Delete
              </button>
            </PermissionGate>
            <button className="btn-cancel" onClick={() => setEditSlot(null)}>Cancel</button>
            <button className="btn-primary" onClick={handleSave} disabled={updateSlot.isPending}>Save</button>
          </>
        }
      >
        {editSlot && form && (
          <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
            <div style={{ padding: 12, background: "#f8f9fc", borderRadius: 8 }}>
              <div style={{ fontWeight: 700, fontSize: 14 }}>{editSlot._courseCode} · Section {editSlot._offering?.sectionCode}</div>
              <div style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>{editSlot._courseTitle}</div>
              {conflicts.has(editSlot.id) && (
                <div style={{ marginTop: 6 }}><StatusBadge status="cancelled" label="Time conflict" /></div>
              )}
            </div>

            <label style={{ fontSize: 12, fontWeight: 600 }}>
              Day
              <select value={form.dayOfWeek} onChange={(e) => setForm((f) => ({ ...f, dayOfWeek: e.target.value }))}
                style={{ width: "100%", marginTop: 4, padding: "8px 10px", border: "1px solid #d1d5db", borderRadius: 8, fontSize: 13 }}>
                {Object.entries(DAY_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </label>

            <div style={{ display: "flex", gap: 10 }}>
              <label style={{ flex: 1, fontSize: 12, fontWeight: 600 }}>
                Start
                <input type="time" value={form.startTime} onChange={(e) => setForm((f) => ({ ...f, startTime: e.target.value }))}
                  style={{ width: "100%", marginTop: 4, padding: "8px 10px", border: "1px solid #d1d5db", borderRadius: 8, fontSize: 13, boxSizing: "border-box" }} />
              </label>
              <label style={{ flex: 1, fontSize: 12, fontWeight: 600 }}>
                End
                <input type="time" value={form.endTime} onChange={(e) => setForm((f) => ({ ...f, endTime: e.target.value }))}
                  style={{ width: "100%", marginTop: 4, padding: "8px 10px", border: "1px solid #d1d5db", borderRadius: 8, fontSize: 13, boxSizing: "border-box" }} />
              </label>
            </div>

            <label style={{ fontSize: 12, fontWeight: 600 }}>
              Kind
              <select value={form.kind} onChange={(e) => setForm((f) => ({ ...f, kind: e.target.value }))}
                style={{ width: "100%", marginTop: 4, padding: "8px 10px", border: "1px solid #d1d5db", borderRadius: 8, fontSize: 13 }}>
                {Object.entries(SLOT_KIND_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </label>

            <label style={{ fontSize: 12, fontWeight: 600 }}>
              Location
              <input type="text" value={form.location} placeholder="e.g. Bldg A, Room 201"
                onChange={(e) => setForm((f) => ({ ...f, location: e.target.value }))}
                style={{ width: "100%", marginTop: 4, padding: "8px 10px", border: "1px solid #d1d5db", borderRadius: 8, fontSize: 13, boxSizing: "border-box" }} />
            </label>

            <label style={{ fontSize: 12, fontWeight: 600 }}>
              Notes
              <textarea value={form.notes} rows={2}
                onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))}
                style={{ width: "100%", marginTop: 4, padding: "8px 10px", border: "1px solid #d1d5db", borderRadius: 8, fontSize: 13, boxSizing: "border-box", fontFamily: "inherit", resize: "vertical" }} />
            </label>
          </div>
        )}
      </Drawer>

      <ConfirmDialog
        open={!!confirmDelete}
        onClose={() => setConfirmDelete(null)}
        onConfirm={handleDelete}
        title="Delete Schedule Slot"
        message="Delete this schedule slot?"
        detail="This permanently removes the slot from the timetable."
        confirmLabel="Yes, Delete"
        variant="danger"
        loading={deleteSlot.isPending}
      />
    </div>
  );
}
