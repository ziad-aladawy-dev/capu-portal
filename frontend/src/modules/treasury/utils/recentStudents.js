const RECENTS_KEY = "treasury.recentStudents";
const MAX_RECENTS = 5;

export function readRecentStudents() {
  try {
    const raw = localStorage.getItem(RECENTS_KEY);
    const list = raw ? JSON.parse(raw) : [];
    return Array.isArray(list) ? list : [];
  } catch {
    return [];
  }
}

export function rememberStudent(student) {
  const slim = {
    id: student.id,
    name: student.name,
    localizedName: student.localizedName,
    studentCode: student.studentCode,
    email: student.email,
  };
  const next = [slim, ...readRecentStudents().filter((s) => s.id !== slim.id)].slice(0, MAX_RECENTS);
  try {
    localStorage.setItem(RECENTS_KEY, JSON.stringify(next));
  } catch {
    /* storage full / unavailable — recents are a convenience only */
  }
}
