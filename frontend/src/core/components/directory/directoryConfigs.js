import { GraduationCap, Users } from "lucide-react";
import { STUDENT_COLUMNS, STAFF_COLUMNS } from "../table/columnDefinitions";
import userService from "../../../modules/users/services/userService";

export const studentDirectoryConfig = {
  id: "student-directory",
  userType: "student",
  icon: GraduationCap,
  titleKey: "student_directory",
  descriptionKey: "manage_and_view_all_students",
  codeField: "studentCode",
  columns: STUDENT_COLUMNS,
  filterFields: ["status", "passwordStatus", "faculty", "program", "level"],
  fetchFn: (params) => userService.getAllStudents(params),
  exportFns: {
    csv: (params) => userService.exportStudentsCsv(params),
    excel: (params) => userService.exportStudentsExcel(params),
  },
  bulkActions: { activate: true, deactivate: true, delete: true },
  routes: {
    add: "/admin/users/students/add",
    edit: (id) => `/admin/users/students/${id}/edit`,
  },
};

export const staffDirectoryConfig = {
  id: "staff-directory",
  userType: "staff",
  icon: Users,
  titleKey: "staff_directory",
  descriptionKey: "manage_and_view_all_staff",
  codeField: "employeeCode",
  columns: STAFF_COLUMNS,
  filterFields: ["status", "passwordStatus", "role", "jobTitle"],
  fetchFn: (params) => userService.getAllStaff(params),
  exportFns: {
    csv: (params) => userService.exportStaffCsv(params),
    excel: (params) => userService.exportStaffExcel(params),
  },
  bulkActions: { activate: true, deactivate: true, delete: true },
  routes: {
    add: "/admin/users/staff/add",
    edit: (id) => `/admin/users/staff/${id}/edit`,
  },
};
