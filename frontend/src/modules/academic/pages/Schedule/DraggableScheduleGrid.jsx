import { useState, useMemo, useCallback } from "react";
import {
  DndContext, useDraggable, useDroppable, DragOverlay,
  PointerSensor, useSensor, useSensors,
} from "@dnd-kit/core";
import { Clock, Plus, Edit2, Trash2, Lock, Unlock, AlertCircle, X, Check, Move } from "lucide-react";
import { DAY_LABELS, SLOT_KIND_LABELS } from "../../../../core/services/scheduleService";
import StatusBadge from "../../../../core/components/StatusBadge";
import PermissionGate from "../../../../core/auth/PermissionGate";
import {
  findOverlappingSlots, getSlotKindClass,
  timeToGridPosition, timeToRowSpan, generateTimeIntervals,
} from "../../../../core/utils/scheduleOverlap";

const START_HOUR = 7;
const END_HOUR = 22;
const intervals = generateTimeIntervals(START_HOUR, END_HOUR);
const days = [0, 1, 2, 3, 4, 5, 6];

function DroppableCell({ day, time, onDrop }) {
  const id = `cell-${day}-${time}`;
  const { setNodeRef, isOver } = useDroppable({ id });

  return (
    <div
      ref={setNodeRef}
      className={`sch-drop-cell${isOver ? " is-over" : ""}`}
      role="gridcell"
      aria-label={`${DAY_LABELS[day]} ${intervals[time] || time}`}
      style={{
        gridRow: parseInt(time) + 2,
        gridColumn: day + 2,
        position: "relative",
      }}
      data-day={day}
      data-time={time}
    />
  );
}

function DraggableSlotBlock({ slot, kindLabel, hasConflict, closed, onEdit, onDelete, onToggleLifecycle, openLoading, closeLoading }) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: `slot-${slot.id}`,
    data: slot,
    disabled: closed,
  });

  const startRow = timeToGridPosition(slot.startTime?.slice(0, 5), START_HOUR);
  const span = timeToRowSpan(slot.startTime?.slice(0, 5), slot.endTime?.slice(0, 5));

  const style = {
    gridRow: `${startRow} / span ${span}`,
    gridColumn: slot.dayOfWeek + 2,
    opacity: closed ? 0.55 : isDragging ? 0.3 : 1,
    filter: closed ? "grayscale(0.6)" : "none",
    cursor: closed ? "default" : "grab",
    borderColor: hasConflict ? "#dc2626" : undefined,
    borderWidth: hasConflict ? 2 : undefined,
    boxShadow: hasConflict ? "0 0 0 2px rgba(220,38,38,0.3)" : isDragging ? "0 4px 12px rgba(0,0,0,0.2)" : undefined,
    transform: transform ? `translate(${transform.x}px, ${transform.y}px)` : undefined,
    zIndex: isDragging ? 100 : undefined,
  };

  return (
    <div
      ref={setNodeRef}
      {...attributes}
      {...listeners}
      className={`sch-slot-block ${getSlotKindClass(slot.kind)} ${hasConflict ? "sch-slot-conflict" : ""}`}
      style={style}
      onClick={() => !closed && onEdit(slot)}
      role="button"
      tabIndex={0}
      aria-label={`${kindLabel} ${slot.startTime?.slice(0, 5)}-${slot.endTime?.slice(0, 5)} ${slot.location || ""}`}
      onKeyDown={(e) => { if (e.key === "Enter" && !closed) onEdit(slot); }}
    >
      <div className="sch-slot-time">
        <Move size={8} style={{ marginRight: 2, opacity: 0.4 }} />
        {slot.startTime?.slice(0, 5)}–{slot.endTime?.slice(0, 5)}
        {hasConflict && <AlertCircle size={8} style={{ marginLeft: 3, color: "#dc2626" }} />}
        {closed && <StatusBadge status="closed" label="CLOSED" style={{ marginLeft: 3, fontSize: 7 }} />}
      </div>
      <div className="sch-slot-location">
        <StatusBadge status={slot.kind === 0 ? "open" : "draft"} label={kindLabel} style={{ fontSize: 8, padding: "1px 4px" }} />
        {slot.location ? ` · ${slot.location}` : ""}
      </div>
      <div className="sch-slot-actions">
        {closed ? (
          <PermissionGate resource="schedule.schedule-slots" minLevel={4}>
            <button className="sch-slot-action-btn" title="Reopen"
              onClick={(e) => { e.stopPropagation(); onToggleLifecycle(slot); }}
              disabled={openLoading}>
              <Unlock size={9} />
            </button>
          </PermissionGate>
        ) : (
          <PermissionGate resource="schedule.schedule-slots" minLevel={3}>
            <button className="sch-slot-action-btn" title="Close"
              onClick={(e) => { e.stopPropagation(); onToggleLifecycle(slot); }}
              disabled={closeLoading}>
              <Lock size={9} />
            </button>
          </PermissionGate>
        )}
        <PermissionGate resource="schedule.schedule-slots" minLevel={3}>
          <button className="sch-slot-action-btn" title="Edit"
            onClick={(e) => { e.stopPropagation(); onEdit(slot); }} disabled={closed}>
            <Edit2 size={9} />
          </button>
        </PermissionGate>
        <PermissionGate resource="schedule.schedule-slots" minLevel={5}>
          <button className="sch-slot-action-btn danger" title="Delete"
            onClick={(e) => { e.stopPropagation(); onDelete(slot); }} disabled={closed}>
            <Trash2 size={9} />
          </button>
        </PermissionGate>
      </div>
    </div>
  );
}

function UnscheduledOffering({ offering, index }) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: `offering-${offering.id}`,
    data: { ...offering, _isOffering: true },
  });

  const style = {
    transform: transform ? `translate(${transform.x}px, ${transform.y}px)` : undefined,
    opacity: isDragging ? 0.4 : 1,
    zIndex: isDragging ? 100 : undefined,
  };

  return (
    <div
      ref={setNodeRef}
      {...attributes}
      {...listeners}
      className="sch-unscheduled-card"
      style={style}
      role="button"
      tabIndex={0}
      aria-label={`Drag ${offering.courseCode} to schedule`}
    >
      <Move size={10} style={{ flexShrink: 0, color: "#9ca3af" }} />
      <div className="sch-unscheduled-info">
        <strong>{offering.courseCode}</strong>
        <span>{offering.courseTitle} ({offering.sectionCode})</span>
      </div>
      <StatusBadge status={offering.isClosed ? "closed" : "open"} label={offering.isClosed ? "Closed" : "Open"} style={{ fontSize: 8 }} />
    </div>
  );
}

export default function DraggableScheduleGrid({
  slots = [],
  offerings = [],
  selectedOfferingId,
  onSelectOffering,
  onEditSlot,
  onDeleteSlot,
  onToggleLifecycle,
  onCreateSlot,
  slotOverlaps = {},
  openLoading,
  closeLoading,
  createLoading,
}) {
  const [dragTarget, setDragTarget] = useState(null);
  const [dropTarget, setDropTarget] = useState(null);
  const [showRoomModal, setShowRoomModal] = useState(false);
  const [selectedRoom, setSelectedRoom] = useState("");
  const [roomError, setRoomError] = useState("");

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: { distance: 5 },
    })
  );

  const scheduledOfferingIds = useMemo(() => {
    const ids = new Set();
    for (const s of slots) ids.add(s.courseOfferingId);
    return ids;
  }, [slots]);

  const unscheduled = useMemo(() => {
    return offerings.filter((o) => !scheduledOfferingIds.has(o.id));
  }, [offerings, scheduledOfferingIds]);

  const handleDragStart = useCallback((event) => {
    setDragTarget(event.active.data.current);
  }, []);

  const handleDragEnd = useCallback((event) => {
    const { active, over } = event;
    setDragTarget(null);

    if (!over || !active.data.current) return;

    const data = active.data.current;
    const dropId = over.id;

    // Check if dropped on a cell
    if (typeof dropId === "string" && dropId.startsWith("cell-")) {
      const parts = dropId.split("-");
      const day = parseInt(parts[1]);
      const timeIdx = parseInt(parts[2]);

      if (data._isOffering) {
        // Dropping a new offering onto the grid
        const time = intervals[timeIdx] || "08:00";
        const endTime = addHour(time);
        const dayOfWeek = day;

        setDropTarget({
          offeringId: data.id,
          dayOfWeek,
          startTime: time,
          endTime,
          kind: 0,
          location: "",
          courseCode: data.courseCode,
          capacity: data.capacity || 0,
        });
        setSelectedRoom("");
        setRoomError("");
        setShowRoomModal(true);
      }
    }
  }, [intervals]);

  const handleDragCancel = useCallback(() => {
    setDragTarget(null);
  }, []);

  const availableRooms = useMemo(() => {
    if (!dropTarget) return ["A101", "A102", "A201", "B101", "B202", "C301", "Lab 1", "Lab 2", "Studio A"];
    // Filter rooms already booked at this time
    const booked = slots
      .filter((s) => s.dayOfWeek === dropTarget.dayOfWeek)
      .filter((s) => {
        if (!s.startTime || !dropTarget.startTime) return false;
        return s.startTime.slice(0, 5) === dropTarget.startTime || s.endTime.slice(0, 5) > dropTarget.startTime;
      })
      .map((s) => s.location)
      .filter(Boolean);

    const allRooms = ["A101", "A102", "A201", "B101", "B202", "C301", "Lab 1", "Lab 2", "Studio A"];
    return allRooms.filter((r) => !booked.includes(r) && !r.toLowerCase().includes("studio"));
  }, [dropTarget, slots]);

  const confirmDrop = () => {
    if (!dropTarget) return;
    if (!selectedRoom) {
      setRoomError("Please select a room");
      return;
    }
    const overlapCheck = findOverlappingSlots(
      {
        dayOfWeek: dropTarget.dayOfWeek,
        startTime: dropTarget.startTime,
        endTime: dropTarget.endTime,
      },
      slots
    );
    if (overlapCheck.length > 0) {
      setRoomError("Time conflict detected. Choose a different time slot.");
      return;
    }

    onCreateSlot({
      courseOfferingId: dropTarget.offeringId,
      dayOfWeek: dropTarget.dayOfWeek,
      startTime: dropTarget.startTime,
      endTime: dropTarget.endTime,
      kind: dropTarget.kind,
      location: selectedRoom,
    });
    setShowRoomModal(false);
    setDropTarget(null);
  };

  return (
    <DndContext
      sensors={sensors}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
      onDragCancel={handleDragCancel}
    >
      <div className="sch-drag-layout">
        {/* Unscheduled sidebar */}
        <div className="sch-unscheduled-sidebar" role="list" aria-label="Unscheduled offerings, drag to timetable">
          <div className="sch-unscheduled-header">
            <Clock size={13} />
            <span>Unscheduled</span>
          </div>
          {unscheduled.length === 0 ? (
            <div className="sch-unscheduled-empty">All offerings scheduled</div>
          ) : (
            <div className="sch-unscheduled-list">
              {unscheduled.map((o, idx) => (
                <UnscheduledOffering key={o.id} offering={o} index={idx} />
              ))}
            </div>
          )}
        </div>

        {/* Timetable grid */}
        <div className="sch-timetable-wrap" role="grid" aria-label="Weekly timetable">
          <div
            className="sch-timetable"
            role="rowgroup"
            style={{
              gridTemplateColumns: `60px repeat(7, 1fr)`,
              gridTemplateRows: `36px repeat(${intervals.length}, 28px)`,
            }}
          >
            <div className="sch-timetable-corner" role="columnheader">Time</div>
            {days.map((d, idx) => (
              <div key={d} className="sch-timetable-day-header" role="columnheader" style={{ gridRow: 1, gridColumn: idx + 2 }}>
                {DAY_LABELS[d]}
              </div>
            ))}

            {intervals.map((time, rowIdx) => (
              <div key={`row-${time}`} role="row" style={{ display: "contents" }}>
                <div className="sch-time-label" role="rowheader" style={{ gridRow: rowIdx + 2, gridColumn: 1 }}>{time}</div>
                {days.map((d, colIdx) => (
                  <DroppableCell key={`cell-${d}-${rowIdx}`} day={d} time={rowIdx} />
                ))}
              </div>
            ))}

            {slots.map((slot) => {
              const kindLabel = SLOT_KIND_LABELS[slot.kind] || "Other";
              const closed = slot.isClosed;
              const hasConflict = slotOverlaps[slot.id]?.length > 0;

              return (
                <DraggableSlotBlock
                  key={slot.id}
                  slot={slot}
                  kindLabel={kindLabel}
                  hasConflict={hasConflict}
                  closed={closed}
                  onEdit={onEditSlot}
                  onDelete={onDeleteSlot}
                  onToggleLifecycle={onToggleLifecycle}
                  openLoading={openLoading}
                  closeLoading={closeLoading}
                />
              );
            })}
          </div>
        </div>
      </div>

      {/* Screen-reader accessible table representation */}
      <table className="sr-only" aria-label="Schedule slots table">
        <thead>
          <tr>
            <th scope="col">Day</th>
            <th scope="col">Start</th>
            <th scope="col">End</th>
            <th scope="col">Kind</th>
            <th scope="col">Room</th>
            <th scope="col">Status</th>
          </tr>
        </thead>
        <tbody>
          {slots.length === 0 ? (
            <tr><td colSpan={6}>No slots scheduled</td></tr>
          ) : (
            slots.map((slot) => {
              const kindLabel = SLOT_KIND_LABELS[slot.kind] || "Other";
              const status = slot.isClosed ? "Closed" : "Open";
              return (
                <tr key={slot.id}>
                  <td>{DAY_LABELS[slot.dayOfWeek]}</td>
                  <td>{slot.startTime?.slice(0, 5)}</td>
                  <td>{slot.endTime?.slice(0, 5)}</td>
                  <td>{kindLabel}</td>
                  <td>{slot.location || "—"}</td>
                  <td>{status}{slotOverlaps[slot.id]?.length > 0 ? " (conflict)" : ""}</td>
                </tr>
              );
            })
          )}
        </tbody>
      </table>

      {/* Drag overlay */}
      <DragOverlay>
        {dragTarget ? (
          <div
            style={{
              padding: "6px 10px",
              background: "#1a1f5e",
              color: "white",
              borderRadius: 8,
              fontSize: 12,
              fontWeight: 600,
              whiteSpace: "nowrap",
              boxShadow: "0 8px 24px rgba(0,0,0,0.2)",
            }}
          >
            {dragTarget.courseCode || dragTarget._isOffering ? dragTarget.courseCode : `${DAY_LABELS[dragTarget.dayOfWeek]} ${dragTarget.startTime?.slice(0,5)}`}
          </div>
        ) : null}
      </DragOverlay>

      {/* Room selection modal */}
      {showRoomModal && dropTarget && (
        <div className="sch-confirm-overlay" onClick={() => { setShowRoomModal(false); setDropTarget(null); }}>
          <div className="sch-confirm-box" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 420 }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
              <h3 style={{ margin: 0, fontSize: 14 }}>Assign Room</h3>
              <button className="btn-cancel" style={{ padding: "4px 8px" }}
                onClick={() => { setShowRoomModal(false); setDropTarget(null); }}>
                <X size={14} />
              </button>
            </div>
            <p style={{ fontSize: 12, color: "#6b7280", margin: "0 0 12px" }}>
              Assign a room for <strong>{dropTarget.courseCode}</strong> on{" "}
              {DAY_LABELS[dropTarget.dayOfWeek]} at {dropTarget.startTime}–{dropTarget.endTime}.
            </p>

            <div className="form-group">
              <label htmlFor="room-select">Room *</label>
              <select
                id="room-select"
                value={selectedRoom}
                onChange={(e) => { setSelectedRoom(e.target.value); setRoomError(""); }}
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }}
              >
                <option value="">Select a room...</option>
                {availableRooms.map((room) => (
                  <option key={room} value={room}>{room}</option>
                ))}
              </select>
              <small style={{ color: "#6b7280", fontSize: 10 }}>
                {availableRooms.length} room(s) available at this time
              </small>
            </div>

            {roomError && (
              <div style={{ padding: "8px 12px", background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 8, color: "#b91c1c", fontSize: 12, marginTop: 8 }}>
                {roomError}
              </div>
            )}

            <div style={{ display: "flex", justifyContent: "flex-end", gap: 8, marginTop: 16 }}>
              <button className="btn-cancel" onClick={() => { setShowRoomModal(false); setDropTarget(null); }}>
                Cancel
              </button>
              <button className="btn-primary" onClick={confirmDrop} disabled={createLoading}>
                <Check size={14} /> {createLoading ? "Scheduling..." : "Schedule Here"}
              </button>
            </div>
          </div>
        </div>
      )}
    </DndContext>
  );
}

function addHour(time) {
  if (!time) return "09:00";
  const [h, m] = time.split(":").map(Number);
  return `${String(h + 1).padStart(2, "0")}:${String(m).padStart(2, "0")}`;
}
