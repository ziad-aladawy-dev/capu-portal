/**
 * @file Pure validation functions for course forms
 */

import { CREDIT_HOURS_MIN, CREDIT_HOURS_MAX, CODE_MAX_LENGTH, TITLE_MAX_LENGTH } from "../constants/courseConstants";

/**
 * Validates a course form object
 * @param {Object} form - The form data
 * @param {string} form.code - Course code
 * @param {string} form.title - Course title
 * @param {number} form.creditHours - Credit hours
 * @param {number} form.category - Category value
 * @returns {Object} Validation result with isValid and errors
 */
export function validateCourseForm(form) {
  const errors = {};

  if (!form.code || !form.code.trim()) {
    errors.code = "courses.codeRequired";
  } else if (form.code.trim().length > CODE_MAX_LENGTH) {
    errors.code = "courses.codeMaxLength";
  }

  if (!form.title || !form.title.trim()) {
    errors.title = "courses.titleRequired";
  } else if (form.title.trim().length > TITLE_MAX_LENGTH) {
    errors.title = "courses.titleMaxLength";
  }

  const credits = Number(form.creditHours);
  if (!Number.isFinite(credits) || credits < CREDIT_HOURS_MIN || credits > CREDIT_HOURS_MAX) {
    errors.creditHours = "courses.creditHoursRange";
  }

  if (form.category === undefined || form.category === null || form.category === "") {
    errors.category = "courses.categoryRequired";
  }

  return {
    isValid: Object.keys(errors).length === 0,
    errors,
  };
}

/**
 * Validates prerequisites for cycles
 * @param {string} courseId - The course being edited
 * @param {Array<string>} prerequisiteIds - Selected prerequisite IDs
 * @param {Array<Object>} allCourses - All courses for cycle detection
 * @param {Function} findCycleFn - Cycle detection function
 * @returns {Object|null} Cycle info or null if valid
 */
export function validatePrerequisites(courseId, prerequisiteIds, allCourses, findCycleFn) {
  if (!courseId || !prerequisiteIds?.length) return null;

  const cycleResult = findCycleFn(courseId, allCourses);
  if (cycleResult?.inCycle) {
    return {
      inCycle: true,
      cycle: cycleResult.cycle,
      message: "courses.prerequisiteCycleDetected",
    };
  }
  return null;
}

/**
 * Sanitizes form data before API submission
 * @param {Object} form - Raw form data
 * @returns {Object} Sanitized data
 */
export function sanitizeCourseForm(form) {
  return {
    code: form.code?.trim(),
    title: form.title?.trim(),
    creditHours: Number(form.creditHours) || 0,
    category: Number(form.category) || 0,
    prerequisites: Array.isArray(form.prerequisites) ? form.prerequisites : [],
  };
}