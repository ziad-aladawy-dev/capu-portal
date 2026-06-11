/**
 * Pure function: checks if a proposed time slot overlaps with existing slots.
 * Returns array of conflicting slots.
 */
export function findOverlappingSlots(proposed, existing) {
  const { dayOfWeek, startTime, endTime, excludeId } = proposed;

  if (!startTime || !endTime || endTime <= startTime) return [];

  const pStart = toMinutes(startTime);
  const pEnd = toMinutes(endTime);

  return existing.filter((slot) => {
    if (excludeId && slot.id === excludeId) return false;
    if (Number(slot.dayOfWeek) !== Number(dayOfWeek)) return false;
    if (slot.isClosed) return false;

    const sStart = toMinutes(slot.startTime);
    const sEnd = toMinutes(slot.endTime);

    // Overlap: intervals intersect
    return pStart < sEnd && pEnd > sStart;
  });
}

/**
 * Check if new slot conflicts with existing ones.
 * Returns true if there's any overlap.
 */
export function hasOverlap(proposed, existing) {
  return findOverlappingSlots(proposed, existing).length > 0;
}

/**
 * Batch-validate multiple proposed slots.
 * Returns map of index → conflicting slots.
 */
export function findAllOverlaps(proposedSlots, existing) {
  const conflicts = {};
  for (let i = 0; i < proposedSlots.length; i++) {
    const overlapping = findOverlappingSlots(proposedSlots[i], existing);
    if (overlapping.length > 0) {
      conflicts[i] = overlapping;
    }
  }
  return conflicts;
}

/**
 * Pairwise-validate a batch of proposed slots against EACH OTHER (the
 * server accepts them one by one, so two overlapping rows in one batch
 * would otherwise half-succeed). Returns map of index → array of other
 * indexes it overlaps.
 */
export function findIntraBatchOverlaps(proposedSlots) {
  const conflicts = {};
  for (let i = 0; i < proposedSlots.length; i++) {
    const a = proposedSlots[i];
    if (!a.startTime || !a.endTime || a.endTime <= a.startTime) continue;
    for (let j = i + 1; j < proposedSlots.length; j++) {
      const b = proposedSlots[j];
      if (!b.startTime || !b.endTime || b.endTime <= b.startTime) continue;
      if (Number(a.dayOfWeek) !== Number(b.dayOfWeek)) continue;
      if (toMinutes(a.startTime) < toMinutes(b.endTime) && toMinutes(a.endTime) > toMinutes(b.startTime)) {
        (conflicts[i] = conflicts[i] || []).push(j);
        (conflicts[j] = conflicts[j] || []).push(i);
      }
    }
  }
  return conflicts;
}

/**
 * Get CSS class for slot kind.
 */
export function getSlotKindClass(kind) {
  const labels = ["lecture", "lab", "tutorial", "seminar", "exam", "other"];
  return `sch-kind-${labels[kind] || "other"}`;
}

/**
 * Convert time string "HH:mm" to minutes since midnight.
 */
export function toMinutes(timeStr) {
  if (!timeStr) return 0;
  const [h, m] = timeStr.split(":").map(Number);
  return h * 60 + m;
}

/**
 * Generate time intervals for the timetable grid.
 * Returns array of "HH:mm" strings at 30-min increments.
 */
export function generateTimeIntervals(startHour = 7, endHour = 22) {
  const intervals = [];
  for (let t = startHour * 60; t < endHour * 60; t += 30) {
    const h = Math.floor(t / 60);
    const m = t % 60;
    intervals.push(`${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}`);
  }
  return intervals;
}

/**
 * Calculate grid position for a time string.
 */
export function timeToGridPosition(timeStr, startHour = 7) {
  const [h, m] = timeStr.split(":").map(Number);
  const totalMin = (h - startHour) * 60 + m;
  return 2 + Math.floor(totalMin / 30);
}

export function timeToRowSpan(startTime, endTime) {
  const [sh, sm] = startTime.split(":").map(Number);
  const [eh, em] = endTime.split(":").map(Number);
  const totalMin = (eh - sh) * 60 + (em - sm);
  return Math.max(1, Math.ceil(totalMin / 30));
}
