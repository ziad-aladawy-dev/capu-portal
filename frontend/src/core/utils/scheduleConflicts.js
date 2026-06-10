/** Time/conflict helpers for the schedule grid and matrix. */

/** Convert "HH:mm" / "HH:mm:ss" / minute-number to minutes since midnight. */
export function toMinutes(t) {
  if (t == null) return 0;
  if (typeof t === "number") return t;
  const [h = "0", m = "0"] = String(t).split(":");
  return Number(h) * 60 + Number(m);
}

function overlaps(a, b) {
  return a.dayOfWeek === b.dayOfWeek &&
    toMinutes(a.startTime) < toMinutes(b.endTime) &&
    toMinutes(b.startTime) < toMinutes(a.endTime);
}

/** Returns the Set of slot ids that conflict (same day, overlapping time) with another slot. */
export function findConflicts(slots) {
  const conflicting = new Set();
  for (let i = 0; i < slots.length; i++) {
    for (let j = i + 1; j < slots.length; j++) {
      if (overlaps(slots[i], slots[j])) {
        conflicting.add(slots[i].id);
        conflicting.add(slots[j].id);
      }
    }
  }
  return conflicting;
}
