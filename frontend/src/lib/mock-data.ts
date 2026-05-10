/**
 * Mock data for static backend simulation
 */

// Mock user data
export const mockUser = {
  id: "user-001",
  name: "Dr. Ahmed Hassan",
  email: "ahmed.hassan@capu.edu.eg",
  attributes: {
    Uni: "Capital University",
    Faculty: "Engineering",
    department: "Computer Science"
  }
};

// Mock colleges/faculties
export const mockColleges = [
  { id: "college-001", name: "Engineering" },
  { id: "college-002", name: "Business" },
  { id: "college-003", name: "Liberal Arts" },
  { id: "college-004", name: "Science" }
];

// Mock programs
export const mockPrograms = [
  { id: "prog-001", name: "Computer Science", collegeId: "college-001" },
  { id: "prog-002", name: "Civil Engineering", collegeId: "college-001" },
  { id: "prog-003", name: "Business Administration", collegeId: "college-002" },
  { id: "prog-004", name: "Economics", collegeId: "college-002" },
  { id: "prog-005", name: "Physics", collegeId: "college-004" },
  { id: "prog-006", name: "Mechanical Engineering", collegeId: "college-001" },
  { id: "prog-007", name: "Electrical Engineering", collegeId: "college-001" },
  { id: "prog-008", name: "Accounting", collegeId: "college-002" },
  { id: "prog-009", name: "Marketing", collegeId: "college-002" },
  { id: "prog-010", name: "Political Science", collegeId: "college-003" },
  { id: "prog-011", name: "Psychology", collegeId: "college-003" },
  { id: "prog-012", name: "Mathematics", collegeId: "college-004" },
  { id: "prog-013", name: "Chemistry", collegeId: "college-004" },
];

// Mock academic years
export const mockAcademicYears = [
  { id: "year-2024", name: "2024" },
  { id: "year-2025", name: "2025" },
  { id: "year-2026", name: "2026" }
];

// Mock semesters
export const mockSemesters = [
  { id: "sem-fall", name: "Fall" },
  { id: "sem-spring", name: "Spring" },
  { id: "sem-summer", name: "Summer" }
];

// Mock authorized scope
export const mockAuthorizedScope = {
  allowedFacultyIds: ["college-001", "college-002", "college-003", "college-004"],
  allowedProgramIds: ["prog-001", "prog-002", "prog-003", "prog-004", "prog-005", "prog-006", "prog-007", "prog-008", "prog-009", "prog-010", "prog-011", "prog-012", "prog-013"],
  allowedAcademicYearIds: ["year-2024", "year-2025", "year-2026"],
  allowedSemesterIds: ["sem-fall", "sem-spring", "sem-summer"]
};

// Mock active scope
export const mockActiveScope = {
  structural: {
    facultyId: "college-001",
    programId: "prog-001"
  },
  temporal: {
    academicYearId: "year-2026",
    semesterId: "sem-fall"
  }
};

// Mock roles
export const mockRoles = [
  {
    id: "role-super-admin",
    name: "Super Administrator",
    description: "Full system access"
  },
  {
    id: "role-college-admin",
    name: "College Administrator",
    description: "Manage college-specific resources"
  },
  {
    id: "role-registrar",
    name: "Registrar",
    description: "Manage registration and enrollment"
  },
  {
    id: "role-financial",
    name: "Financial Officer",
    description: "Manage financial operations"
  },
  {
    id: "role-faculty",
    name: "Faculty",
    description: "View and manage student data"
  }
];

// Mock permissions (default role permissions)
export const mockRolePermissions = {
  "role-super-admin": [
    { module: "Students", resource: "Profile", level: 5, scope: { collegeScope: "all", timeScope: "all" } },
    { module: "Students", resource: "Enrollment", level: 5, scope: { collegeScope: "all", timeScope: "all" } },
    { module: "Admin", resource: "Users", level: 5, scope: { collegeScope: "all", timeScope: "all" } },
    { module: "Admin", resource: "Roles", level: 5, scope: { collegeScope: "all", timeScope: "all" } },
    { module: "Financial", resource: "Billing", level: 5, scope: { collegeScope: "all", timeScope: "all" } },
    { module: "Registration", resource: "Courses", level: 5, scope: { collegeScope: "all", timeScope: "all" } }
  ],
  "role-college-admin": [
    { module: "Students", resource: "Profile", level: 3, scope: { collegeScope: "specific", collegeIds: ["college-001"], timeScope: "all" } },
    { module: "Students", resource: "Enrollment", level: 3, scope: { collegeScope: "specific", collegeIds: ["college-001"], timeScope: "all" } },
    { module: "Admin", resource: "Users", level: 2, scope: { collegeScope: "specific", collegeIds: ["college-001"], timeScope: "all" } },
    { module: "Registration", resource: "Courses", level: 2, scope: { collegeScope: "specific", collegeIds: ["college-001"], timeScope: "all" } }
  ],
  "role-registrar": [
    { module: "Students", resource: "Profile", level: 2, scope: { collegeScope: "all", timeScope: "all" } },
    { module: "Students", resource: "Enrollment", level: 4, scope: { collegeScope: "all", timeScope: "all" } },
    { module: "Registration", resource: "Courses", level: 3, scope: { collegeScope: "all", timeScope: "all" } },
    { module: "Registration", resource: "Registration", level: 4, scope: { collegeScope: "all", timeScope: "all" } }
  ],
  "role-financial": [
    { module: "Students", resource: "Profile", level: 1, scope: { collegeScope: "all", timeScope: "all" } },
    { module: "Financial", resource: "Billing", level: 4, scope: { collegeScope: "all", timeScope: "all" } },
    { module: "Financial", resource: "Payments", level: 4, scope: { collegeScope: "all", timeScope: "all" } }
  ],
  "role-faculty": [
    { module: "Students", resource: "Profile", level: 1, scope: { collegeScope: "all", timeScope: "all" } },
    { module: "Students", resource: "Grades", level: 3, scope: { collegeScope: "all", timeScope: "all" } }
  ]
};

// Mock users with role assignments
export const mockUsers = [
  {
    id: "admin-001",
    name: "Dr. Ahmed Hassan",
    email: "ahmed.hassan@capu.edu.eg",
    roleId: "role-super-admin",
    permissions: mockRolePermissions["role-super-admin"],
    moduleVisibility: ["Students", "Admin", "Financial", "Registration", "Permissions"]
  },
  {
    id: "admin-002",
    name: "Eng. Fatima Khalil",
    email: "fatima.khalil@capu.edu.eg",
    roleId: "role-college-admin",
    permissions: mockRolePermissions["role-college-admin"],
    permissionOverrides: [
      // Example override: add Financial.Billing view
      { module: "Financial", resource: "Billing", level: 1, type: "added" }
    ],
    moduleVisibility: ["Students", "Admin", "Registration", "Financial"]
  },
  {
    id: "admin-003",
    name: "Ms. Nour Amin",
    email: "nour.amin@capu.edu.eg",
    roleId: "role-registrar",
    permissions: mockRolePermissions["role-registrar"],
    permissionOverrides: [],
    moduleVisibility: ["Students", "Registration"]
  }
];

// Mock permission matrix for display
export const mockPermissionMatrix = [
  {
    module: "Students",
    icon: "Users",
    resources: [
      { name: "Profile", level: 5 },
      { name: "Enrollment", level: 5 },
      { name: "Grades", level: 3 },
      { name: "Schedule", level: 2 }
    ]
  },
  {
    module: "Admin",
    icon: "Shield",
    resources: [
      { name: "Users", level: 5 },
      { name: "Roles", level: 5 },
      { name: "Departments", level: 4 },
      { name: "Faculties", level: 3 }
    ]
  },
  {
    module: "Financial",
    icon: "DollarSign",
    resources: [
      { name: "Billing", level: 5 },
      { name: "Payments", level: 5 },
      { name: "Scholarships", level: 3 },
      { name: "Reports", level: 2 }
    ]
  },
  {
    module: "Registration",
    icon: "BookOpen",
    resources: [
      { name: "Courses", level: 5 },
      { name: "Sections", level: 4 },
      { name: "Registration", level: 5 },
      { name: "Waitlist", level: 3 }
    ]
  }
];

// Mock token
export const mockToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c2VyLTAwMSIsImlhdCI6MTUxNjIzOTAyMn0.8c2bKl8F8K5zF8K5zF8K5zF8K5zF8K5zF8K5zF8K5z";

// ==================== STUDENT MOCK DATA ====================

export interface Student {
  id: string;
  studentId: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  dateOfBirth: string;
  gender: "Male" | "Female";
  address: string;
  nationality: string;
  collegeId: string;
  programId: string;
  academicYearId: string;
  semesterId: string;
  enrollmentStatus: "Active" | "Inactive" | "Graduated" | "Suspended" | "Graduation Pending";
  enrollmentDate: string;
  profileImage?: string;
  gpa: number;
  totalCredits: number;
  financialStatus: "Paid" | "Pending" | "Partial" | "Overdue";
  guardianName: string;
  guardianPhone: string;
}

export interface CourseEnrollment {
  id: string;
  studentId: string;
  courseId: string;
  courseName: string;
  courseCode: string;
  credits: number;
  grade?: string;
  gradePoints?: number;
  semester: string;
  academicYear: string;
  status: "Enrolled" | "Completed" | "Dropped" | "Failed";
  attendancePercentage: number;
}

export interface Course {
  id: string;
  code: string;
  name: string;
  credits: number;
  collegeId: string;
  programId: string;
  semesterId: string;
  academicYearId: string;
  instructor: string;
  schedule: string;
  room: string;
}

// Rich student data
export const mockStudents: Student[] = [
  {
    id: "stu-001",
    studentId: "ENG2024001",
    firstName: "Ahmed",
    lastName: "Mohamed",
    email: "ahmed.mohamed@student.capu.edu.eg",
    phone: "+20 100 123 4567",
    dateOfBirth: "2002-05-15",
    gender: "Male",
    address: "123 Nasser City, Cairo",
    nationality: "Egyptian",
    collegeId: "college-001",
    programId: "prog-001",
    academicYearId: "year-2026",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2024-09-01",
    gpa: 3.85,
    totalCredits: 98,
    financialStatus: "Paid",
    guardianName: "Mohamed Ahmed",
    guardianPhone: "+20 100 987 6543"
  },
  {
    id: "stu-002",
    studentId: "ENG2024002",
    firstName: "Sara",
    lastName: "Ali",
    email: "sara.ali@student.capu.edu.eg",
    phone: "+20 101 234 5678",
    dateOfBirth: "2003-02-20",
    gender: "Female",
    address: "45 Heliopolis, Cairo",
    nationality: "Egyptian",
    collegeId: "college-001",
    programId: "prog-001",
    academicYearId: "year-2026",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2024-09-01",
    gpa: 3.92,
    totalCredits: 102,
    financialStatus: "Paid",
    guardianName: "Ali Hassan",
    guardianPhone: "+20 101 876 5432"
  },
  {
    id: "stu-003",
    studentId: "ENG2024003",
    firstName: "Omar",
    lastName: "Ibrahim",
    email: "omar.ibrahim@student.capu.edu.eg",
    phone: "+20 102 345 6789",
    dateOfBirth: "2002-11-08",
    gender: "Male",
    address: "78 Maadi, Cairo",
    nationality: "Egyptian",
    collegeId: "college-001",
    programId: "prog-001",
    academicYearId: "year-2026",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2024-09-01",
    gpa: 3.45,
    totalCredits: 94,
    financialStatus: "Partial",
    guardianName: "Ibrahim Gamal",
    guardianPhone: "+20 102 765 4321"
  },
  {
    id: "stu-004",
    studentId: "BUS2024001",
    firstName: "Fatima",
    lastName: "Khaled",
    email: "fatima.khaled@student.capu.edu.eg",
    phone: "+20 103 456 7890",
    dateOfBirth: "2003-07-12",
    gender: "Female",
    address: "32 Zamalek, Cairo",
    nationality: "Egyptian",
    collegeId: "college-002",
    programId: "prog-003",
    academicYearId: "year-2026",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2024-09-01",
    gpa: 3.78,
    totalCredits: 88,
    financialStatus: "Paid",
    guardianName: "Khaled Mahmoud",
    guardianPhone: "+20 103 654 3210"
  },
  {
    id: "stu-005",
    studentId: "BUS2024002",
    firstName: "Youssef",
    lastName: "Tarek",
    email: "youssef.tarek@student.capu.edu.eg",
    phone: "+20 104 567 8901",
    dateOfBirth: "2002-03-25",
    gender: "Male",
    address: "56 Giza, Cairo",
    nationality: "Egyptian",
    collegeId: "college-002",
    programId: "prog-003",
    academicYearId: "year-2026",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2024-09-01",
    gpa: 3.12,
    totalCredits: 76,
    financialStatus: "Pending",
    guardianName: "Tarek Hassan",
    guardianPhone: "+20 104 543 2109"
  },
  {
    id: "stu-006",
    studentId: "ENG2023001",
    firstName: "Mariam",
    lastName: "Nabil",
    email: "mariam.nabil@student.capu.edu.eg",
    phone: "+20 105 678 9012",
    dateOfBirth: "2001-09-30",
    gender: "Female",
    address: "89 Dokki, Cairo",
    nationality: "Egyptian",
    collegeId: "college-001",
    programId: "prog-001",
    academicYearId: "year-2025",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2023-09-01",
    gpa: 3.95,
    totalCredits: 142,
    financialStatus: "Paid",
    guardianName: "Nabil Saad",
    guardianPhone: "+20 105 432 1098"
  },
  {
    id: "stu-007",
    studentId: "ENG2023002",
    firstName: "Karim",
    lastName: "Walid",
    email: "karim.walid@student.capu.edu.eg",
    phone: "+20 106 789 0123",
    dateOfBirth: "2002-12-14",
    gender: "Male",
    address: "11 Mohandessin, Cairo",
    nationality: "Egyptian",
    collegeId: "college-001",
    programId: "prog-002",
    academicYearId: "year-2025",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2023-09-01",
    gpa: 3.28,
    totalCredits: 136,
    financialStatus: "Overdue",
    guardianName: "Walid Kamel",
    guardianPhone: "+20 106 321 0987"
  },
  {
    id: "stu-008",
    studentId: "SCI2024001",
    firstName: "Lina",
    lastName: "Hossam",
    email: "lina.hossam@student.capu.edu.eg",
    phone: "+20 107 890 1234",
    dateOfBirth: "2003-04-18",
    gender: "Female",
    address: "67 New Cairo, Cairo",
    nationality: "Egyptian",
    collegeId: "college-004",
    programId: "prog-005",
    academicYearId: "year-2026",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2024-09-01",
    gpa: 3.67,
    totalCredits: 82,
    financialStatus: "Paid",
    guardianName: "Hossam El Din",
    guardianPhone: "+20 107 210 9876"
  },
  {
    id: "stu-009",
    studentId: "ENG2022001",
    firstName: "Tarek",
    lastName: "Sherif",
    email: "tarek.sherif@student.capu.edu.eg",
    phone: "+20 108 901 2345",
    dateOfBirth: "2000-08-05",
    gender: "Male",
    address: "90 Nasr City, Cairo",
    nationality: "Egyptian",
    collegeId: "college-001",
    programId: "prog-001",
    academicYearId: "year-2024",
    semesterId: "sem-fall",
    enrollmentStatus: "Graduated",
    enrollmentDate: "2022-09-01",
    gpa: 3.55,
    totalCredits: 156,
    financialStatus: "Paid",
    guardianName: "Sherif Magdy",
    guardianPhone: "+20 108 109 8765"
  },
  {
    id: "stu-010",
    studentId: "BUS2023001",
    firstName: "Nour",
    lastName: "Amir",
    email: "nour.amir@student.capu.edu.eg",
    phone: "+20 109 012 3456",
    dateOfBirth: "2002-01-22",
    gender: "Female",
    address: "44 Garden City, Cairo",
    nationality: "Egyptian",
    collegeId: "college-002",
    programId: "prog-004",
    academicYearId: "year-2025",
    semesterId: "sem-fall",
    enrollmentStatus: "Suspended",
    enrollmentDate: "2023-09-01",
    gpa: 2.85,
    totalCredits: 68,
    financialStatus: "Pending",
    guardianName: "Amir Fouad",
    guardianPhone: "+20 109 098 7654"
  },
  // Business College Students
  {
    id: "stu-011",
    studentId: "BUS2026001",
    firstName: "Mohamed",
    lastName: "Salem",
    email: "mohamed.salem@student.capu.edu.eg",
    phone: "+20 110 111 2222",
    dateOfBirth: "2003-08-10",
    gender: "Male",
    address: "55 Mohandessin, Cairo",
    nationality: "Egyptian",
    collegeId: "college-002",
    programId: "prog-003",
    academicYearId: "year-2026",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2024-09-01",
    gpa: 3.55,
    totalCredits: 72,
    financialStatus: "Paid",
    guardianName: "Salem Ibrahim",
    guardianPhone: "+20 110 222 3333"
  },
  {
    id: "stu-012",
    studentId: "BUS2026002",
    firstName: "Hana",
    lastName: "Rashad",
    email: "hana.rashad@student.capu.edu.eg",
    phone: "+20 111 222 3333",
    dateOfBirth: "2003-03-18",
    gender: "Female",
    address: "66 Heliopolis, Cairo",
    nationality: "Egyptian",
    collegeId: "college-002",
    programId: "prog-008",
    academicYearId: "year-2026",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2024-09-01",
    gpa: 3.72,
    totalCredits: 66,
    financialStatus: "Paid",
    guardianName: "Rashad Mohamed",
    guardianPhone: "+20 111 333 4444"
  },
  // Engineering Students - Other Programs
  {
    id: "stu-013",
    studentId: "ENG2025001",
    firstName: "Omar",
    lastName: "Farouk",
    email: "omar.farouk@student.capu.edu.eg",
    phone: "+20 112 333 4444",
    dateOfBirth: "2002-07-25",
    gender: "Male",
    address: "77 Dokki, Cairo",
    nationality: "Egyptian",
    collegeId: "college-001",
    programId: "prog-006",
    academicYearId: "year-2025",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2023-09-01",
    gpa: 3.22,
    totalCredits: 118,
    financialStatus: "Paid",
    guardianName: "Farouk Kamal",
    guardianPhone: "+20 112 444 5555"
  },
  {
    id: "stu-014",
    studentId: "ENG2026003",
    firstName: "Sarah",
    lastName: "Nasser",
    email: "sarah.nasser@student.capu.edu.eg",
    phone: "+20 113 444 5555",
    dateOfBirth: "2003-11-30",
    gender: "Female",
    address: "88 Maadi, Cairo",
    nationality: "Egyptian",
    collegeId: "college-001",
    programId: "prog-007",
    academicYearId: "year-2026",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2024-09-01",
    gpa: 3.88,
    totalCredits: 54,
    financialStatus: "Paid",
    guardianName: "Nasser Hassan",
    guardianPhone: "+20 113 555 6666"
  },
  // Liberal Arts Students
  {
    id: "stu-015",
    studentId: "ART2025001",
    firstName: "Yasmin",
    lastName: "Talaat",
    email: "yasmin.talaat@student.capu.edu.eg",
    phone: "+20 114 555 6666",
    dateOfBirth: "2002-04-12",
    gender: "Female",
    address: "99 Zamalek, Cairo",
    nationality: "Egyptian",
    collegeId: "college-003",
    programId: "prog-010",
    academicYearId: "year-2025",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2023-09-01",
    gpa: 3.41,
    totalCredits: 92,
    financialStatus: "Partial",
    guardianName: "Talaat Ahmed",
    guardianPhone: "+20 114 666 7777"
  },
  // Science Students
  {
    id: "stu-016",
    studentId: "SCI2026001",
    firstName: "Ali",
    lastName: "Sayed",
    email: "ali.sayed@student.capu.edu.eg",
    phone: "+20 115 666 7777",
    dateOfBirth: "2003-06-22",
    gender: "Male",
    address: "100 Giza, Cairo",
    nationality: "Egyptian",
    collegeId: "college-004",
    programId: "prog-005",
    academicYearId: "year-2026",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2024-09-01",
    gpa: 3.75,
    totalCredits: 48,
    financialStatus: "Paid",
    guardianName: "Sayed Mahmoud",
    guardianPhone: "+20 115 777 8888"
  },
  {
    id: "stu-017",
    studentId: "SCI2025001",
    firstName: "Mona",
    lastName: "Khaled",
    email: "mona.khaled@student.capu.edu.eg",
    phone: "+20 116 777 8888",
    dateOfBirth: "2002-09-08",
    gender: "Female",
    address: "111 New Cairo, Cairo",
    nationality: "Egyptian",
    collegeId: "college-004",
    programId: "prog-012",
    academicYearId: "year-2025",
    semesterId: "sem-fall",
    enrollmentStatus: "Active",
    enrollmentDate: "2023-09-01",
    gpa: 3.33,
    totalCredits: 86,
    financialStatus: "Pending",
    guardianName: "Khaled Adel",
    guardianPhone: "+20 116 888 9999"
  }
];

// Mock course enrollments with grades
export const mockCourseEnrollments: CourseEnrollment[] = [
  // Ahmed Mohamed (stu-001) - Fall 2026
  { id: "enr-001", studentId: "stu-001", courseId: "cs101", courseName: "Introduction to Programming", courseCode: "CS101", credits: 3, grade: "A", gradePoints: 4.0, semester: "Fall", academicYear: "2026", status: "Completed", attendancePercentage: 95 },
  { id: "enr-002", studentId: "stu-001", courseId: "cs102", courseName: "Data Structures", courseCode: "CS102", credits: 4, grade: "A-", gradePoints: 3.7, semester: "Fall", academicYear: "2026", status: "Enrolled", attendancePercentage: 88 },
  { id: "enr-003", studentId: "stu-001", courseId: "math201", courseName: "Linear Algebra", courseCode: "MATH201", credits: 3, grade: "B+", gradePoints: 3.3, semester: "Fall", academicYear: "2026", status: "Enrolled", attendancePercentage: 92 },
  { id: "enr-004", studentId: "stu-001", courseId: "eng101", courseName: "Technical Writing", courseCode: "ENG101", credits: 2, grade: "A", gradePoints: 4.0, semester: "Fall", academicYear: "2026", status: "Enrolled", attendancePercentage: 100 },
  // Previous semesters
  { id: "enr-005", studentId: "stu-001", courseId: "cs201", courseName: "Algorithms", courseCode: "CS201", credits: 4, grade: "A-", gradePoints: 3.7, semester: "Spring", academicYear: "2025", status: "Completed", attendancePercentage: 90 },
  { id: "enr-006", studentId: "stu-001", courseId: "math101", courseName: "Calculus I", courseCode: "MATH101", credits: 3, grade: "B+", gradePoints: 3.3, semester: "Spring", academicYear: "2025", status: "Completed", attendancePercentage: 85 },

  // Sara Ali (stu-002) - Fall 2026
  { id: "enr-007", studentId: "stu-002", courseId: "cs101", courseName: "Introduction to Programming", courseCode: "CS101", credits: 3, grade: "A+", gradePoints: 4.0, semester: "Fall", academicYear: "2026", status: "Completed", attendancePercentage: 98 },
  { id: "enr-008", studentId: "stu-002", courseId: "cs102", courseName: "Data Structures", courseCode: "CS102", credits: 4, grade: "A", gradePoints: 4.0, semester: "Fall", academicYear: "2026", status: "Enrolled", attendancePercentage: 95 },
  { id: "enr-009", studentId: "stu-002", courseId: "math201", courseName: "Linear Algebra", courseCode: "MATH201", credits: 3, grade: "A-", gradePoints: 3.7, semester: "Fall", academicYear: "2026", status: "Enrolled", attendancePercentage: 92 },
  { id: "enr-010", studentId: "stu-002", courseId: "phys101", courseName: "Physics I", courseCode: "PHYS101", credits: 3, grade: "A", gradePoints: 4.0, semester: "Fall", academicYear: "2026", status: "Enrolled", attendancePercentage: 90 },

  // Omar Ibrahim (stu-003) - Fall 2026
  { id: "enr-011", studentId: "stu-003", courseId: "cs101", courseName: "Introduction to Programming", courseCode: "CS101", credits: 3, grade: "B+", gradePoints: 3.3, semester: "Fall", academicYear: "2026", status: "Completed", attendancePercentage: 75 },
  { id: "enr-012", studentId: "stu-003", courseId: "cs102", courseName: "Data Structures", courseCode: "CS102", credits: 4, grade: "B", gradePoints: 3.0, semester: "Fall", academicYear: "2026", status: "Enrolled", attendancePercentage: 70 },
  { id: "enr-013", studentId: "stu-003", courseId: "math201", courseName: "Linear Algebra", courseCode: "MATH201", credits: 3, grade: "B-", gradePoints: 2.7, semester: "Fall", academicYear: "2026", status: "Enrolled", attendancePercentage: 65 },

  // Fatima Khaled (stu-004) - Fall 2026
  { id: "enr-014", studentId: "stu-004", courseId: "bus101", courseName: "Principles of Business", courseCode: "BUS101", credits: 3, grade: "A-", gradePoints: 3.7, semester: "Fall", academicYear: "2026", status: "Completed", attendancePercentage: 92 },
  { id: "enr-015", studentId: "stu-004", courseId: "acc101", courseName: "Introduction to Accounting", courseCode: "ACC101", credits: 3, grade: "A", gradePoints: 4.0, semester: "Fall", academicYear: "2026", status: "Enrolled", attendancePercentage: 88 },
  { id: "enr-016", studentId: "stu-004", courseId: "eco101", courseName: "Principles of Economics", courseCode: "ECO101", credits: 3, grade: "A", gradePoints: 4.0, semester: "Fall", academicYear: "2026", status: "Enrolled", attendancePercentage: 95 },

  // Mariam Nabil (stu-006) - Fall 2025
  { id: "enr-017", studentId: "stu-006", courseId: "cs301", courseName: "Database Systems", courseCode: "CS301", credits: 4, grade: "A+", gradePoints: 4.0, semester: "Fall", academicYear: "2025", status: "Completed", attendancePercentage: 98 },
  { id: "enr-018", studentId: "stu-006", courseId: "cs302", courseName: "Software Engineering", courseCode: "CS302", credits: 3, grade: "A", gradePoints: 4.0, semester: "Fall", academicYear: "2025", status: "Completed", attendancePercentage: 95 },
  { id: "enr-019", studentId: "stu-006", courseId: "cs303", courseName: "Computer Networks", courseCode: "CS303", credits: 3, grade: "A-", gradePoints: 3.7, semester: "Fall", academicYear: "2025", status: "Completed", attendancePercentage: 90 },
];

// Mock courses
export const mockCourses: Course[] = [
  { id: "cs101", code: "CS101", name: "Introduction to Programming", credits: 3, collegeId: "college-001", programId: "prog-001", semesterId: "sem-fall", academicYearId: "year-2026", instructor: "Dr. Islam", schedule: "Sun-Tue 10:00-12:00", room: "Lab 1" },
  { id: "cs102", code: "CS102", name: "Data Structures", credits: 4, collegeId: "college-001", programId: "prog-001", semesterId: "sem-fall", academicYearId: "year-2026", instructor: "Dr. Nabil", schedule: "Mon-Wed 14:00-16:00", room: "Lab 2" },
  { id: "math201", code: "MATH201", name: "Linear Algebra", credits: 3, collegeId: "college-001", programId: "prog-001", semesterId: "sem-fall", academicYearId: "year-2026", instructor: "Dr. Hany", schedule: "Sun-Tue 08:00-10:00", room: "Hall A" },
  { id: "eng101", code: "ENG101", name: "Technical Writing", credits: 2, collegeId: "college-001", programId: "prog-001", semesterId: "sem-fall", academicYearId: "year-2026", instructor: "Dr. Dina", schedule: "Thu 10:00-12:00", room: "Hall B" },
  { id: "phys101", code: "PHYS101", name: "Physics I", credits: 3, collegeId: "college-001", programId: "prog-001", semesterId: "sem-fall", academicYearId: "year-2026", instructor: "Dr. Sherif", schedule: "Mon-Wed 12:00-14:00", room: "Lab 3" },
  { id: "bus101", code: "BUS101", name: "Principles of Business", credits: 3, collegeId: "college-002", programId: "prog-003", semesterId: "sem-fall", academicYearId: "year-2026", instructor: "Dr. Amr", schedule: "Sun-Tue 14:00-16:00", room: "Hall C" },
  { id: "acc101", code: "ACC101", name: "Introduction to Accounting", credits: 3, collegeId: "college-002", programId: "prog-003", semesterId: "sem-fall", academicYearId: "year-2026", instructor: "Dr. Omar", schedule: "Mon-Wed 10:00-12:00", room: "Hall A" },
  { id: "eco101", code: "ECO101", name: "Principles of Economics", credits: 3, collegeId: "college-002", programId: "prog-003", semesterId: "sem-fall", academicYearId: "year-2026", instructor: "Dr. Laila", schedule: "Sun-Tue 16:00-18:00", room: "Hall B" },
  { id: "cs201", code: "CS201", name: "Algorithms", credits: 4, collegeId: "college-001", programId: "prog-001", semesterId: "sem-spring", academicYearId: "year-2025", instructor: "Dr. Islam", schedule: "Sun-Tue 10:00-12:00", room: "Lab 1" },
  { id: "math101", code: "MATH101", name: "Calculus I", credits: 3, collegeId: "college-001", programId: "prog-001", semesterId: "sem-spring", academicYearId: "year-2025", instructor: "Dr. Hany", schedule: "Mon-Wed 08:00-10:00", room: "Hall A" },
  { id: "cs301", code: "CS301", name: "Database Systems", credits: 4, collegeId: "college-001", programId: "prog-001", semesterId: "sem-fall", academicYearId: "year-2025", instructor: "Dr. Nabil", schedule: "Sun-Tue 14:00-16:00", room: "Lab 2" },
  { id: "cs302", code: "CS302", name: "Software Engineering", credits: 3, collegeId: "college-001", programId: "prog-001", semesterId: "sem-fall", academicYearId: "year-2025", instructor: "Dr. Rania", schedule: "Mon-Wed 10:00-12:00", room: "Lab 1" },
  { id: "cs303", code: "CS303", name: "Computer Networks", credits: 3, collegeId: "college-001", programId: "prog-001", semesterId: "sem-fall", academicYearId: "year-2025", instructor: "Dr. Islam", schedule: "Thu 12:00-14:00", room: "Lab 3" },
];

// Helper functions
export const getStudentById = (id: string): Student | undefined => 
  mockStudents.find(s => s.id === id);

export const getStudentsByProgram = (programId: string): Student[] =>
  mockStudents.filter(s => s.programId === programId);

export const getStudentsByCollege = (collegeId: string): Student[] =>
  mockStudents.filter(s => s.collegeId === collegeId);

export const getStudentEnrollments = (studentId: string): CourseEnrollment[] =>
  mockCourseEnrollments.filter(e => e.studentId === studentId);

export const getProgramById = (id: string) => mockPrograms.find(p => p.id === id);
export const getCollegeById = (id: string) => mockColleges.find(c => c.id === id);
export const getYearById = (id: string) => mockAcademicYears.find(y => y.id === id);
export const getSemesterById = (id: string) => mockSemesters.find(s => s.id === id);
