export const ACTION_LEVELS = {
  NONE: 0,
  VIEW: 1,
  INSERT: 2,
  EDIT_CLOSE: 3,
  OPEN: 4,
  DELETE: 5,
};

export const ACTION_LEVEL_LABELS = {
  [ACTION_LEVELS.NONE]: "None",
  [ACTION_LEVELS.VIEW]: "View",
  [ACTION_LEVELS.INSERT]: "Insert",
  [ACTION_LEVELS.EDIT_CLOSE]: "Edit + Close",
  [ACTION_LEVELS.OPEN]: "Open",
  [ACTION_LEVELS.DELETE]: "Delete",
};

export const PAGE_TYPES = {
  ENTITY: "entity",
  MANAGEMENT: "management",
};

export const APPLICABLE_TO = {
  BOTH: "both",
  STUDENT: "student",
  STAFF: "staff",
};
