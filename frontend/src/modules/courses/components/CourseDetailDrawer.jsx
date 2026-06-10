import { useState, useMemo } from "react";
import {
  Layers, Clock, MapPin, ChevronDown, ChevronRight, Send, Ban,
  CalendarDays, AlertTriangle, RefreshCw,
} from "lucide-react";
import Drawer from "../../../core/components/Drawer";
import StatusBadge from "../../../core/components/StatusBadge";
import CapacityBar from "../../../core/components/CapacityBar";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import PermissionGate from "../../../core/auth/PermissionGate";
import { useToast } from "../../../core/components/Toast";
import { SkeletonRow } from "../../../core/components/Skeleton";
import { useOfferingsForCourse, useBulkPublishOfferings, useBulkCancelOfferings } from "../../../core/query/useCourseOfferings";
import { useScheduleSlots } from "../../../core/query/useScheduleSlots";
import {
  OFFERING_STATUS_LABELS, REGISTRATION_STATE_LABELS,
} from "../../../core/services/courseOfferingService";
import { SLOT_KIND_LABELS, DAY_LABELS } from "../../../core/services/scheduleService";
import { getCourseCategoryLabel } from "../../../core/services/courseService";

const statusVariant = (s) => ({ 0: "draft", 1: "open", 2: "closed", 3: "cancelled" }[s] || "inactive");
const regVariant = (s) => ({ 0: "closed", 1: "open", 2: "warning" }[s] || "inactive");
const fmtTime = (t) => (t ? String(t).slice(0, 5) : "—");

function OfferingSlots({ offeringId }) {
  const { data: slots = [], isLoading } = useScheduleSlots(offeringId);
  if (isLoading) return <div style={{ padding: "8px 0" }}><SkeletonRow cols={3} /></div>;
  if (slots.length === 0) return <div className="ch-empty-inline">No schedule slots assigned.</div>;
  return (
    <div>
      {slots
        .slice()
        .sort((a, b) => a.dayOfWeek - b.dayOfWeek || String(a.startTime).localeCompare(String(b.startTime)))
        .map((s) => (
          <div key={s.id} className="ch-slot-line">
            <span className="ch-slot-day">{(DAY_LABELS[s.dayOfWeek] || "").slice(0, 3)}</span>
            <Clock size={12} style={{ color: "#9ca3af", flexShrink: 0 }} />
            <span style={{ fontVariantNumeric: "tabular-nums" }}>{fmtTime(s.startTime)}–{fmtTime(s.endTime)}</span>
            <span className="ch-slot-kind">{SLOT_KIND_LABELS[s.kind] || "Slot"}</span>
            {s.location && (
              <span style={{ display: "inline-flex", alignItems: "center", gap: 3, color: "#6b7280" }}>
                <MapPin size={11} /> {s.location}
              </span>
            )}
            {s.isClosed && <StatusBadge status="closed" label="Closed" />}
          </div>
        ))}
    </div>
  );
}

export default function CourseDetailDrawer({ open, onClose, course, semester }) {
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
      n.has(id) ? n.delete(id) : n.add(id);
      return n;
    });

  const handlePublish = async () => {
    const ids = [...selected];
    if (!ids.length) return;
    try {
      await bulkPublish.mutateAsync(ids);
      addToast(`${ids.length} offering(s) published`, "success");
      setSelected(new Set());
    } catch (e) {
      addToast(e.message || "Publish failed", "error");
    }
  };

  const handleCancel = async () => {
    const ids = [...selected];
    if (!ids.length || !cancelReason.trim()) return;
    try {
      await bulkCancel.mutateAsync({ ids, reason: cancelReason });
      addToast(`${ids.length} offering(s) cancelled`, "success");
      setSelected(new Set());
      setCancelReason("");
      setCancelOpen(false);
    } catch (e) {
      addToast(e.message || "Cancel failed", "error");
    }
  };

  if (!course) return null;

  return (
    <Drawer open={open} onClose={onClose} title={`${course.code} — Course Detail`} width={620}>
      <div className="ch-detail-head">
        <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
          <span className="ch-code-pill">{course.code}</span>
          <strong style={{ fontSize: 15 }}>{course.title}</strong>
        </div>
        <div className="ch-detail-meta">
          <span className="ch-meta-chip">{course.creditHours} credit hrs</span>
          <span className="ch-meta-chip">{getCourseCategoryLabel(course.category)}</span>
          <StatusBadge status={course.isActive ? "active" : "inactive"} />
          {course.isClosed && <StatusBadge status="closed" label="Record Closed" />}
          <span className="ch-meta-chip" style={{ display: "inline-flex", alignItems: "center", gap: 4 }}>
            <CalendarDays size={11} /> {semester?.name || "No semester"}
          </span>
        </div>
      </div>

      {/* Offering rollup */}
      <div style={{ display: "flex", gap: 16, marginBottom: 14, flexWrap: "wrap" }}>
        <div><div style={{ fontSize: 18, fontWeight: 700 }}>{stats.count}</div><div className="ch-stat-label">Sections</div></div>
        <div><div style={{ fontSize: 18, fontWeight: 700 }}>{stats.filledSeats}/{stats.totalSeats}</div><div className="ch-stat-label">Seats filled</div></div>
        <div><div style={{ fontSize: 18, fontWeight: 700, color: stats.full ? "#b91c1c" : "#1a1f5e" }}>{stats.full}</div><div className="ch-stat-label">Full sections</div></div>
      </div>

      <div className="ch-section-title">
        <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
          <Layers size={14} /> Offerings — {semester?.name || "current semester"}
        </span>
        {selected.size > 0 && <span style={{ fontSize: 12, color: "#6b7280" }}>{selected.size} selected</span>}
      </div>

      {!semester && (
        <div className="ch-empty-inline">Select a semester in the context bar to view offerings.</div>
      )}

      {error && (
        <div role="alert" style={{ display: "flex", alignItems: "center", gap: 8, padding: 10, background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 8, color: "#b91c1c", fontSize: 12, marginBottom: 10 }}>
          <AlertTriangle size={14} /> Failed to load offerings.
          <button className="btn-cancel" style={{ marginLeft: "auto", padding: "3px 8px", fontSize: 11 }} onClick={() => refetch()}>
            <RefreshCw size={11} /> Retry
          </button>
        </div>
      )}

      {isLoading && semester && (
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          <SkeletonRow cols={4} /><SkeletonRow cols={4} /><SkeletonRow cols={4} />
        </div>
      )}

      {!isLoading && semester && offerings.length === 0 && !error && (
        <div className="ch-empty-inline">No offerings for this course in {semester.name}.</div>
      )}

      {offerings.map((o) => {
        const isOpen = expanded === o.id;
        const isSel = selected.has(o.id);
        return (
          <div key={o.id} className={`ch-offering${isSel ? " selected" : ""}`}>
            <div className="ch-offering-row">
              <input
                type="checkbox"
                checked={isSel}
                onChange={(e) => { e.stopPropagation(); toggleSelect(o.id); }}
                onClick={(e) => e.stopPropagation()}
                aria-label={`Select section ${o.sectionCode}`}
              />
              <div className="ch-offering-main" onClick={() => setExpanded(isOpen ? null : o.id)}>
                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <span className="ch-offering-section">Section {o.sectionCode}</span>
                  <StatusBadge status={statusVariant(o.status)} label={OFFERING_STATUS_LABELS[o.status]} />
                  <StatusBadge status={regVariant(o.registrationState)} label={REGISTRATION_STATE_LABELS[o.registrationState]} />
                </div>
                <div className="ch-offering-sub">Registration: {REGISTRATION_STATE_LABELS[o.registrationState]} · ID {String(o.id).slice(0, 8)}</div>
              </div>
              <CapacityBar registered={o.registeredCount} capacity={o.capacity} width={130} />
              <button
                className="btn-cancel"
                style={{ padding: "4px 6px" }}
                onClick={() => setExpanded(isOpen ? null : o.id)}
                aria-label={isOpen ? "Collapse" : "Expand"}
              >
                {isOpen ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
              </button>
            </div>
            {isOpen && (
              <div className="ch-offering-slots">
                <OfferingSlots offeringId={o.id} />
              </div>
            )}
          </div>
        );
      })}

      {/* Bulk action bar inside drawer */}
      {selected.size > 0 && (
        <PermissionGate resource="course-offerings.course-offerings" minLevel={3}>
          <div style={{ display: "flex", gap: 8, marginTop: 14, padding: 10, background: "#f8f9fc", borderRadius: 8, alignItems: "center" }}>
            <span style={{ fontSize: 12, color: "#6b7280" }}>{selected.size} selected</span>
            <button className="btn-primary" style={{ marginLeft: "auto" }} onClick={handlePublish} disabled={bulkPublish.isPending}>
              <Send size={13} /> Publish
            </button>
            <button className="btn-cancel" onClick={() => setCancelOpen(true)} disabled={bulkCancel.isPending}>
              <Ban size={13} /> Cancel
            </button>
          </div>
        </PermissionGate>
      )}

      <ConfirmDialog
        open={cancelOpen}
        onClose={() => { setCancelOpen(false); setCancelReason(""); }}
        onConfirm={handleCancel}
        title="Cancel Selected Offerings"
        message={`Cancel ${selected.size} offering(s)?`}
        detail="Provide a cancellation reason."
        confirmLabel="Confirm Cancel"
        variant="danger"
        loading={bulkCancel.isPending}
      >
        <div style={{ marginTop: 12 }}>
          <input
            type="text"
            placeholder="Enter cancellation reason…"
            value={cancelReason}
            onChange={(e) => setCancelReason(e.target.value)}
            style={{ width: "100%", padding: "8px 12px", border: "1px solid #d1d5db", borderRadius: 8, fontSize: 13, boxSizing: "border-box" }}
            autoFocus
          />
        </div>
      </ConfirmDialog>
    </Drawer>
  );
}
