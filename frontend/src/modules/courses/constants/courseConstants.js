/**
 * @file Course constants and configuration
 */

export const COURSE_CATEGORIES = [
  { value: 0, labelKey: "courses.categories.0" },
  { value: 1, labelKey: "courses.categories.1" },
  { value: 2, labelKey: "courses.categories.2" },
  { value: 3, labelKey: "courses.categories.3" },
  { value: 4, labelKey: "courses.categories.4" },
  { value: 5, labelKey: "courses.categories.5" },
];

export const COURSE_CATEGORY_VALUES = COURSE_CATEGORIES.map((c) => c.value);

export const CREDIT_HOURS_MIN = 0;
export const CREDIT_HOURS_MAX = 30;
export const CODE_MAX_LENGTH = 32;
export const TITLE_MAX_LENGTH = 200;

export const PAGE_SIZE = 20;
export const MAX_PREREQUISITES_FETCH = 500;

export const EMPTY_FORM = {
  code: "",
  title: "",
  creditHours: 3,
  category: 0,
  prerequisites: [],
};

export const DRAWER_WIDTH = 480;

export const PERMISSIONS = {
  VIEW: "courses.courses.view",
  CREATE: "courses.courses.create",
  EDIT: "courses.courses.edit",
  CLOSE: "courses.courses.close",
  DELETE: "courses.courses.delete",
};

export const PERMISSION_LEVELS = {
  VIEW: 1,
  CREATE: 2,
  EDIT: 3,
  CLOSE: 4,
  DELETE: 5,
};