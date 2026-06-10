import {
  Building2,
  GraduationCap,
  Layers,
  BookOpen,
  Award,
  FolderTree,
  Sparkles,
  Users,
  CalendarRange,
  Calendar,
} from "lucide-react";

export const NODE_TYPES = {
  University: 1,
  Faculty: 2,
  System: 3,
  Program: 4,
  Level: 5,
  Department: 6,
  Specialization: 7,
};

export const NODE_TYPE_VALUES = {
  1: "University",
  2: "Faculty",
  3: "System",
  4: "Program",
  5: "Level",
  6: "Department",
  7: "Specialization",
};

export const NODE_TYPE_CONFIG = {
  1: {
    type: "University",
    icon: Building2,
    color: "#1a237e",
    backgroundColor: "rgba(26,35,126,0.08)",
    borderColor: "rgba(26,35,126,0.2)",
    labelKey: "type_university",
    capabilities: ["isRoot", "hasGlobalConfig"],
    metadataFields: [],
    allowedChildren: [NODE_TYPES.Faculty],
  },
  2: {
    type: "Faculty",
    icon: GraduationCap,
    color: "#b71c1c",
    backgroundColor: "rgba(183,28,28,0.08)",
    borderColor: "rgba(183,28,28,0.2)",
    labelKey: "type_faculty",
    capabilities: ["hasStaff"],
    metadataFields: ["deanId", "establishedDate"],
    allowedChildren: [NODE_TYPES.System, NODE_TYPES.Program],
  },
  3: {
    type: "System",
    icon: Layers,
    color: "#e65100",
    backgroundColor: "rgba(230,81,0,0.08)",
    borderColor: "rgba(230,81,0,0.2)",
    labelKey: "type_system",
    capabilities: ["hasFeeStructure"],
    metadataFields: [],
    allowedChildren: [NODE_TYPES.Program],
  },
  4: {
    type: "Program",
    icon: BookOpen,
    color: "#1b5e20",
    backgroundColor: "rgba(27,94,32,0.08)",
    borderColor: "rgba(27,94,32,0.2)",
    labelKey: "type_program",
    capabilities: ["hasPlans", "hasCurriculum"],
    metadataFields: ["durationYears", "degreeType"],
    allowedChildren: [NODE_TYPES.Level, NODE_TYPES.Specialization],
  },
  5: {
    type: "Level",
    icon: Award,
    color: "#01579b",
    backgroundColor: "rgba(1,87,155,0.08)",
    borderColor: "rgba(1,87,155,0.2)",
    labelKey: "type_level",
    capabilities: ["hasStudents", "hasCourseOfferings"],
    metadataFields: [],
    allowedChildren: [NODE_TYPES.Level, NODE_TYPES.Specialization],
  },
  6: {
    type: "Department",
    icon: FolderTree,
    color: "#4a148c",
    backgroundColor: "rgba(74,20,140,0.08)",
    borderColor: "rgba(74,20,140,0.2)",
    labelKey: "type_department",
    capabilities: ["hasStaff", "managesCourses"],
    metadataFields: [],
    allowedChildren: [],
  },
  7: {
    type: "Specialization",
    icon: Sparkles,
    color: "#33691e",
    backgroundColor: "rgba(51,105,30,0.08)",
    borderColor: "rgba(51,105,30,0.2)",
    labelKey: "type_specialization",
    capabilities: ["hasStudents"],
    metadataFields: [],
    allowedChildren: [NODE_TYPES.Level],
  },
};

export function getNodeTypeConfig(typeValue) {
  if (typeValue === null || typeValue === undefined) return null;
  if (typeof typeValue === "string") {
    const numeric = NODE_TYPES[typeValue];
    if (numeric) return NODE_TYPE_CONFIG[numeric];
    return null;
  }
  return NODE_TYPE_CONFIG[typeValue] || null;
}

export function getNodeTypeLabel(typeValue) {
  const config = getNodeTypeConfig(typeValue);
  return config ? config.type : "Unknown";
}

export function getNodeTypeValue(typeLabel) {
  return NODE_TYPES[typeLabel] || NODE_TYPES.Program;
}

export function normalizeType(type) {
  if (typeof type === "string") return type;
  return getNodeTypeLabel(type);
}

export function getAllowedChildTypes(parentType) {
  const config = getNodeTypeConfig(parentType);
  if (!config) return [];
  return config.allowedChildren.map((v) => NODE_TYPE_VALUES[v]);
}

export function canMoveToParent(nodeType, targetParentType) {
  if (!targetParentType) {
    const config = getNodeTypeConfig(nodeType);
    return config ? config.capabilities.includes("isRoot") : false;
  }
  const allowed = getAllowedChildTypes(targetParentType);
  return allowed.includes(normalizeType(nodeType));
}

export function getNodeCapabilities(typeValue) {
  const config = getNodeTypeConfig(typeValue);
  return config ? config.capabilities : [];
}

export function nodeHasCapability(typeValue, capability) {
  const capabilities = getNodeCapabilities(typeValue);
  return capabilities.includes(capability);
}

const CAPABILITY_ROUTES = {
  hasStudents: { labelKey: "students", path: "/admin/students", icon: Users },
  hasStaff: { labelKey: "staff", path: "/admin/staff", icon: Users },
  hasPlans: { labelKey: "academic_plans", path: "/admin/academic-plans", icon: CalendarRange },
  hasCourseOfferings: { labelKey: "course_offerings", path: "/admin/academic/course-offerings", icon: Calendar },
  hasCurriculum: { labelKey: "academic_plans", path: "/admin/academic-plans", icon: BookOpen },
  managesCourses: { labelKey: "course_catalog", path: "/admin/courses", icon: BookOpen },
};

export function getContextualActions(typeValue) {
  const config = getNodeTypeConfig(typeValue);
  if (!config) return [];
  const seen = new Set();
  return config.capabilities
    .filter(cap => CAPABILITY_ROUTES[cap])
    .filter(cap => {
      const path = CAPABILITY_ROUTES[cap].path;
      if (seen.has(path)) return false;
      seen.add(path);
      return true;
    })
    .map(cap => ({ ...CAPABILITY_ROUTES[cap], capability: cap }));
}

export function getNodeTypePermission(typeValue, action) {
  const config = getNodeTypeConfig(typeValue);
  if (!config) return null;
  const typeLabel = config.type.toLowerCase();
  return `structure.${typeLabel}.${action}`;
}

export function getNodeMetadataFields(typeValue) {
  const config = getNodeTypeConfig(typeValue);
  return config ? config.metadataFields : [];
}

export const ALL_NODE_TYPES_LIST = Object.entries(NODE_TYPES).map(([label, value]) => ({
  value: label,
  numericValue: value,
  labelKey: NODE_TYPE_CONFIG[value].labelKey,
}));
