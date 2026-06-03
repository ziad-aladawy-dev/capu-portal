import { GraduationCap, Building2, BookOpen, UserCircle2, Activity, ChevronRight } from "lucide-react";

export const statsConfig = [
  { key: "total_students", icon: GraduationCap, trendKey: "trend_up", colorClass: "students" },
  { key: "faculties", icon: Building2, trendKey: "trend_stable", colorClass: "faculties" },
  { key: "active_courses", icon: BookOpen, trendKey: "trend_up_courses", colorClass: "courses" },
  { key: "faculty_members", icon: UserCircle2, trendKey: "trend_up_faculty", colorClass: "instructors" },
];

export const iconColors = {
  students: { background: "rgba(26,31,94,0.08)", color: "#1a1f5e" },
  faculties: { background: "rgba(201,168,76,0.12)", color: "#7a5c10" },
  courses: { background: "rgba(96,165,250,0.12)", color: "#2563eb" },
  instructors: { background: "rgba(244,114,182,0.12)", color: "#be185d" },
};

export const statsValues = {
  total_students: "8,945",
  faculties: "12",
  active_courses: "342",
  faculty_members: "456",
};

export const recentActivitiesData = [
  { id: 1, actionKey: "new_student_registration", user: "Ahmed Hassan", timeKey: "minutes_ago", timeValue: 5, dot: "#16a34a" },
  { id: 2, actionKey: "course_updated", user: "Dr. Ali Ibrahim", timeKey: "minutes_ago", timeValue: 12, dot: "#c9a84c", courseName: "AI" },
  { id: 3, actionKey: "new_faculty_added", user: "Faculty of Pharmacy", timeKey: "hour_ago", timeValue: 1, dot: "#2e3591" },
  { id: 4, actionKey: "instructor_profile_updated", user: "Dr. Sara Nour", timeKey: "hours_ago", timeValue: 2, dot: "#c9a84c" },
];

export const quickActionsConfig = [
  { labelKey: "manage_departments", path: "/admin/departments" },
  { labelKey: "add_new_user", path: "/admin/users/add" },
  { labelKey: "add_new_faculty", path: "/admin/faculties/add" },
  { labelKey: "add_new_course", path: "/admin/courses/add" },
  { labelKey: "view_reports", path: "/admin/reports" },
];