const wait = (ms = 250) => new Promise((resolve) => setTimeout(resolve, ms));

const faculties = [
  { id: "f1", name: "Computer Science", nameEn: "Computer Science" },
  { id: "f2", name: "Engineering", nameEn: "Engineering" },
  { id: "f3", name: "Business", nameEn: "Business" },
];

const departments = [
  { id: "d1", facultyId: "f1", name: "Software Engineering", nameEn: "Software Engineering" },
  { id: "d2", facultyId: "f1", name: "Artificial Intelligence", nameEn: "Artificial Intelligence" },
  { id: "d3", facultyId: "f2", name: "Civil Engineering", nameEn: "Civil Engineering" },
  { id: "d4", facultyId: "f3", name: "Accounting", nameEn: "Accounting" },
];

const levels = [
  { id: "l1", name: "Level 1", nameEn: "Level 1" },
  { id: "l2", name: "Level 2", nameEn: "Level 2" },
  { id: "l3", name: "Level 3", nameEn: "Level 3" },
  { id: "l4", name: "Level 4", nameEn: "Level 4" },
];

const roles = [
  { id: "r1", name: "Admin", nameEn: "Admin" },
  { id: "r2", name: "Instructor", nameEn: "Instructor" },
  { id: "r3", name: "Employee", nameEn: "Employee" },
];

let students = [
  {
    id: "s1",
    nationalId: "11111111111111",
    studentCode: "STU-1001",
    fullNameEn: "Ahmed Hassan",
    fullNameAr: "أحمد حسن",
    email: "ahmed@student.edu",
    phone: "01000000001",
    facultyId: "f1",
    facultyName: "Computer Science",
    programId: "d1",
    programName: "Software Engineering",
    levelId: "l2",
    levelName: "Level 2",
    gpa: 3.4,
    isActive: true,
    isDeleted: false,
  },
  {
    id: "s2",
    nationalId: "22222222222222",
    studentCode: "STU-1002",
    fullNameEn: "Mariam Ali",
    fullNameAr: "مريم علي",
    email: "mariam@student.edu",
    phone: "01000000002",
    facultyId: "f2",
    facultyName: "Engineering",
    programId: "d3",
    programName: "Civil Engineering",
    levelId: "l3",
    levelName: "Level 3",
    gpa: 3.7,
    isActive: true,
    isDeleted: false,
  },
];

let staff = [
  {
    id: "st1",
    nationalId: "33333333333333",
    staffCode: "EMP-2001",
    fullNameEn: "Dr. Sara Nour",
    fullNameAr: "د. سارة نور",
    email: "sara@capital.edu",
    phone: "01000000003",
    facultyId: "f1",
    facultyName: "Computer Science",
    staffRoleId: "r2",
    staffRoleName: "Instructor",
    universityName: "Capital University",
    isActive: true,
    isDeleted: false,
  },
  {
    id: "st2",
    nationalId: "44444444444444",
    staffCode: "EMP-2002",
    fullNameEn: "Omar Adel",
    fullNameAr: "عمر عادل",
    email: "omar@capital.edu",
    phone: "01000000004",
    facultyId: "f3",
    facultyName: "Business",
    staffRoleId: "r1",
    staffRoleName: "Admin",
    universityName: "Capital University",
    isActive: true,
    isDeleted: false,
  },
];

const paginate = (items, params = {}) => {
  const pageNumber = Number(params.pageNumber || 1);
  const pageSize = Number(params.pageSize || 10);
  const search = (params.searchTerm || "").toLowerCase();

  const filtered = items.filter((item) => {
    if (!params.includeDeleted && item.isDeleted) return false;
    if (search) {
      return [item.fullNameEn, item.fullNameAr, item.email, item.nationalId, item.studentCode, item.staffCode]
        .filter(Boolean)
        .some((v) => String(v).toLowerCase().includes(search));
    }
    return true;
  });

  const start = (pageNumber - 1) * pageSize;
  return {
    items: filtered.slice(start, start + pageSize),
    pageNumber,
    pageSize,
    totalCount: filtered.length,
    totalPages: Math.max(1, Math.ceil(filtered.length / pageSize)),
  };
};

const userService = {
  async getAllStudents(params) { await wait(); return paginate(students, params); },
  async getAllStaff(params) { await wait(); return paginate(staff, params); },
  async getStudentById(id) { await wait(); return students.find((x) => x.id === id) || null; },
  async getStaffById(id) { await wait(); return staff.find((x) => x.id === id) || null; },
  async getFaculties() { await wait(); return faculties; },
  async getDepartments(facultyId) { await wait(); return facultyId ? departments.filter((d) => d.facultyId === facultyId) : departments; },
  async getLevels() { await wait(); return levels; },
  async getRoles() { await wait(); return roles; },
  async getUniversities() { await wait(); return [{ id: "u1", name: "Capital University", nameEn: "Capital University" }]; },
  async getUserStatistics() {
    await wait();
    return {
      totalUsers: students.length + staff.length,
      totalStudents: students.length,
      totalStaff: staff.length,
      activeUsers: [...students, ...staff].filter((u) => u.isActive).length,
      inactiveUsers: [...students, ...staff].filter((u) => !u.isActive).length,
    };
  },
  async createStudent(data) {
    await wait();
    const item = { id: `s${Date.now()}`, studentCode: `STU-${1000 + students.length + 1}`, isActive: true, isDeleted: false, ...data };
    students.unshift(item);
    return { success: true, id: item.id, data: item };
  },
  async createStaff(data) {
    await wait();
    const item = { id: `st${Date.now()}`, staffCode: `EMP-${2000 + staff.length + 1}`, isActive: true, isDeleted: false, ...data };
    staff.unshift(item);
    return { success: true, id: item.id, data: item };
  },
  async updateStudent(id, data) { await wait(); students = students.map((x) => x.id === id ? { ...x, ...data } : x); return { success: true, id }; },
  async updateStaff(id, data) { await wait(); staff = staff.map((x) => x.id === id ? { ...x, ...data } : x); return { success: true, id }; },
  async activateUser(id) { await wait(); [...students, ...staff].forEach((x) => { if (x.id === id) x.isActive = true; }); return { success: true }; },
  async deactivateUser(id) { await wait(); [...students, ...staff].forEach((x) => { if (x.id === id) x.isActive = false; }); return { success: true }; },
  async softDeleteUser(id) { await wait(); [...students, ...staff].forEach((x) => { if (x.id === id) x.isDeleted = true; }); return { success: true }; },
  async restoreUser(id) { await wait(); [...students, ...staff].forEach((x) => { if (x.id === id) x.isDeleted = false; }); return { success: true }; },
  async resetUserPassword() { await wait(); return { success: true }; },
  async checkEmailUnique(email) { await wait(); return ![...students, ...staff].some((u) => u.email === email); },
  async checkNationalIdUnique(nationalId) { await wait(); return ![...students, ...staff].some((u) => u.nationalId === nationalId); },
  async generateStaffCode() { await wait(); return `EMP-${2000 + staff.length + 1}`; },
};

export default userService;
