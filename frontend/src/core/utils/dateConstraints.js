export function constrainDateWithin(dateStr, minDateStr, maxDateStr) {
  const d = new Date(dateStr);
  const min = minDateStr ? new Date(minDateStr) : null;
  const max = maxDateStr ? new Date(maxDateStr) : null;
  if (min && d < min) return min.toISOString().slice(0, 10);
  if (max && d > max) return max.toISOString().slice(0, 10);
  return dateStr;
}

export function getSemesterDateBounds(yearStart, yearEnd) {
  return {
    minDate: yearStart?.slice(0, 10) || "",
    maxDate: yearEnd?.slice(0, 10) || "",
  };
}

export function autoGenerateSemesters(yearStart, yearEnd) {
  if (!yearStart || !yearEnd) return [];
  const start = new Date(yearStart);
  const end = new Date(yearEnd);
  const mid1 = new Date(start.getTime() + (end - start) / 3);
  const mid2 = new Date(start.getTime() + (2 * (end - start)) / 3);

  return [
    {
      name: "Fall",
      startDate: start.toISOString().slice(0, 10),
      endDate: new Date(mid1.getTime() - 86400000).toISOString().slice(0, 10),
    },
    {
      name: "Spring",
      startDate: mid1.toISOString().slice(0, 10),
      endDate: new Date(mid2.getTime() - 86400000).toISOString().slice(0, 10),
    },
    {
      name: "Summer",
      startDate: mid2.toISOString().slice(0, 10),
      endDate: end.toISOString().slice(0, 10),
    },
  ];
}
