import {
  GraduationCap,
  Building2,
  BookOpen,
  UserCircle2,
} from "lucide-react";

export const stats = [
  {
    label: "Total Students",
    value: "8,945",
    icon: GraduationCap,
    trend: "+12% from last month",
    trendColor: "#16a34a",
    iconClass: "students",
  },
  {
    label: "Faculties",
    value: "12",
    icon: Building2,
    trend: "Stable",
    trendColor: "#c9a84c",
    iconClass: "faculties",
  },
  {
    label: "Active Courses",
    value: "342",
    icon: BookOpen,
    trend: "+5% from last month",
    trendColor: "#16a34a",
    iconClass: "courses",
  },
  {
    label: "Faculty Members",
    value: "456",
    icon: UserCircle2,
    trend: "+2 this month",
    trendColor: "#16a34a",
    iconClass: "instructors",
  },
];

export const recentActivities = [
  {
    id: 1,
    action: "New student registration",
    user: "Ahmed Hassan",
    time: "5 minutes ago",
    dot: "#16a34a",
  },
  {
    id: 2,
    action: "Course 'AI' updated",
    user: "Dr. Ali Ibrahim",
    time: "12 minutes ago",
    dot: "#c9a84c",
  },
  {
    id: 3,
    action: "New faculty added",
    user: "Faculty of Pharmacy",
    time: "1 hour ago",
    dot: "#2e3591",
  },
  {
    id: 4,
    action: "Instructor profile updated",
    user: "Dr. Sara Nour",
    time: "2 hours ago",
    dot: "#c9a84c",
  },
];

export const quickActions = [
  { label: "Manage Departments", path: "/admin/departments" },
  { label: "Add New User", path: "/admin/users/add" },
  { label: "Add New Faculty", path: "/admin/faculties/add" },
  { label: "Add New Course", path: "/admin/courses/add" },
  { label: "View Reports", path: "/admin/reports" },
];

export const iconColors = {
  students: {
    background: "rgba(26,31,94,0.08)",
    color: "#1a1f5e",
  },
  faculties: {
    background: "rgba(201,168,76,0.12)",
    color: "#7a5c10",
  },
  courses: {
    background: "rgba(96,165,250,0.12)",
    color: "#2563eb",
  },
  instructors: {
    background: "rgba(244,114,182,0.12)",
    color: "#be185d",
  },
};