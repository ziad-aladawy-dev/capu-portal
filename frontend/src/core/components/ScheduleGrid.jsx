import { useMemo } from "react";
import { toMinutes, findConflicts } from "../utils/scheduleConflicts";

/**
 * ScheduleGrid — a visual day×time timetable primitive.
 * Lays out schedule slots as positioned blocks across day columns and flags
 * time/location conflicts. Pure presentational: parent supplies slots + handlers.
 *
 * Each slot: { id, dayOfWeek (0=Sun..6=Sat), startTime, endTime, ...meta }
 * Times accept "HH:mm" / "HH:mm:ss" strings or minute numbers.
 *
 * Props:
 *  - slots: Slot[]
 *  - days?: number[]          working days to render (default Sun..Thu)
 *  - startHour?: number       grid start (default 8)
 *  - endHour?: number         grid end (default 20)
 *  - hourHeight?: number      px per hour (default 56)
 *  - renderSlot: (slot, ctx) => ReactNode   ctx = { conflict: boolean }
 *  - onSlotClick?: (slot) => void
 *  - dayLabels?: Record<number,string>
 */
const DEFAULT_DAY_LABELS = {
  0: "Sun", 1: "Mon", 2: "Tue", 3: "Wed", 4: "Thu", 5: "Fri", 6: "Sat",
};

export default function ScheduleGrid({
  slots = [],
  days = [0, 1, 2, 3, 4],
  startHour = 8,
  endHour = 20,
  hourHeight = 56,
  renderSlot,
  onSlotClick,
  dayLabels = DEFAULT_DAY_LABELS,
}) {
  // Include any day that has slots, even outside the default working week.
  const activeDays = useMemo(() => {
    const set = new Set(days);
    slots.forEach((s) => set.add(s.dayOfWeek));
    return [...set].sort((a, b) => a - b);
  }, [days, slots]);

  const conflicts = useMemo(() => findConflicts(slots), [slots]);

  const hours = useMemo(() => {
    const out = [];
    for (let h = startHour; h <= endHour; h++) out.push(h);
    return out;
  }, [startHour, endHour]);

  const gridStart = startHour * 60;
  const gridHeight = (endHour - startHour) * hourHeight;
  const TIME_COL = 56;

  const slotsByDay = useMemo(() => {
    const map = {};
    activeDays.forEach((d) => (map[d] = []));
    slots.forEach((s) => {
      if (map[s.dayOfWeek]) map[s.dayOfWeek].push(s);
    });
    return map;
  }, [slots, activeDays]);

  return (
    <div style={{ overflowX: "auto", border: "1px solid #e5e7eb", borderRadius: 10, background: "white" }}>
      <div style={{ display: "grid", gridTemplateColumns: `${TIME_COL}px repeat(${activeDays.length}, minmax(140px, 1fr))`, minWidth: TIME_COL + activeDays.length * 140 }}>
        {/* Header row */}
        <div style={{ borderBottom: "1px solid #e5e7eb", background: "#f8f9fc" }} />
        {activeDays.map((d) => (
          <div
            key={`h-${d}`}
            style={{
              borderBottom: "1px solid #e5e7eb",
              borderLeft: "1px solid #f3f4f6",
              background: "#f8f9fc",
              padding: "8px 10px",
              fontSize: 12,
              fontWeight: 700,
              color: "#1a1f5e",
              textAlign: "center",
            }}
          >
            {dayLabels[d] || d}
          </div>
        ))}

        {/* Time gutter */}
        <div style={{ position: "relative", height: gridHeight }}>
          {hours.map((h, i) => (
            <div
              key={`t-${h}`}
              style={{
                position: "absolute",
                top: i * hourHeight - 6,
                right: 6,
                fontSize: 10,
                color: "#9ca3af",
                fontVariantNumeric: "tabular-nums",
              }}
            >
              {String(h).padStart(2, "0")}:00
            </div>
          ))}
        </div>

        {/* Day columns */}
        {activeDays.map((d) => (
          <div
            key={`c-${d}`}
            style={{
              position: "relative",
              height: gridHeight,
              borderLeft: "1px solid #f3f4f6",
              backgroundImage: `repeating-linear-gradient(to bottom, transparent, transparent ${hourHeight - 1}px, #f3f4f6 ${hourHeight - 1}px, #f3f4f6 ${hourHeight}px)`,
            }}
          >
            {slotsByDay[d].map((s) => {
              const top = ((toMinutes(s.startTime) - gridStart) / 60) * hourHeight;
              const h = Math.max(18, ((toMinutes(s.endTime) - toMinutes(s.startTime)) / 60) * hourHeight - 3);
              const isConflict = conflicts.has(s.id);
              return (
                <div
                  key={s.id}
                  onClick={() => onSlotClick?.(s)}
                  style={{
                    position: "absolute",
                    top: Math.max(0, top),
                    left: 3,
                    right: 3,
                    height: h,
                    cursor: onSlotClick ? "pointer" : "default",
                    zIndex: isConflict ? 2 : 1,
                  }}
                >
                  {renderSlot(s, { conflict: isConflict })}
                </div>
              );
            })}
          </div>
        ))}
      </div>
    </div>
  );
}
