/**
 * Permission constants and utilities
 */

export const PERMISSION_LEVELS = [
  { id: 0, name: "None", description: "No access" },
  { id: 1, name: "Read", description: "View records only" },
  { id: 2, name: "Write", description: "Create without history tracking" },
  { id: 3, name: "Edit", description: "Modify existing records & lock" },
  { id: 4, name: "Open", description: "Unlock and full operational access" },
  { id: 5, name: "Delete", description: "Remove records entirely" }
];

export const PERMISSION_ACTIONS = [
  { level: 1, name: "Read", color: "#3b82f6" },
  { level: 2, name: "Write", color: "#8b5cf6" },
  { level: 3, name: "Edit", color: "#e0c06a" },
  { level: 4, name: "Open", color: "#10b981" },
  { level: 5, name: "Delete", color: "#ef4444" }
];

/**
 * Check if user has a specific permission level
 * Uses cumulative logic: if user has level 3, they have 1, 2, and 3
 */
export const hasPermission = (userLevel: number, requiredLevel: number) => {
  return userLevel >= requiredLevel;
};

/**
 * Get permission level by ID
 */
export const getPermissionLevel = (levelId: number) => {
  return PERMISSION_LEVELS.find(p => p.id === levelId);
};

/**
 * Get all actions available at a given level (cumulative)
 */
export const getAvailableActions = (level: number) => {
  if (level === 0) return [];
  return PERMISSION_ACTIONS.filter(a => a.level <= level);
};

/**
 * Format permission for display
 */
export const formatPermissionLevel = (level: number) => {
  const perm = getPermissionLevel(level);
  return perm ? perm.name : "Unknown";
};

// Filter categories
export const FILTER_CATEGORIES = {
  STUDENTS: "students",
  ADMIN: "admin",
  FINANCIAL: "financial",
  REGISTRATION: "registration"
};

export const CATEGORY_LABELS = {
  [FILTER_CATEGORIES.STUDENTS]: "Students",
  [FILTER_CATEGORIES.ADMIN]: "Admin Management",
  [FILTER_CATEGORIES.FINANCIAL]: "Financial",
  [FILTER_CATEGORIES.REGISTRATION]: "Registration"
};

// Default filter state per category
export const getDefaultFiltersForCategory = (category) => {
  return {
    collegeId: null,
    programTypeId: null,
    programId: null,
    academicYearId: null,
    semesterId: null,
    entityId: null // student/admin/staff ID depending on category
  };
};

// Modules list
export const MODULES = {
  DASHBOARD: "Dashboard",
  STUDENTS: "Students",
  ADMIN: "Admin Management",
  FINANCIAL: "Financial",
  REGISTRATION: "Registration",
  PERMISSIONS: "Permissions",
  SETTINGS: "Settings"
};

// Resources per module
export const MODULE_RESOURCES = {
  [MODULES.STUDENTS]: [
    "Profile",
    "Enrollment",
    "Grades",
    "Schedule"
  ],
  [MODULES.ADMIN]: [
    "Users",
    "Roles",
    "Departments",
    "Faculties"
  ],
  [MODULES.FINANCIAL]: [
    "Billing",
    "Payments",
    "Scholarships",
    "Reports"
  ],
  [MODULES.REGISTRATION]: [
    "Courses",
    "Sections",
    "Registration",
    "Waitlist"
  ]
};
