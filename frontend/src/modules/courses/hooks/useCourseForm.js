import { useState, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { validateCourseForm, sanitizeCourseForm } from "../utils/courseValidators";
import { findCoursePrerequisiteCycle } from "../utils/dagValidator";
import { EMPTY_FORM } from "../constants/courseConstants";

export function useCourseForm({ initialForm = EMPTY_FORM, editCourse = null, allCourses = [] }) {
  const { t } = useTranslation("courses");
  const [form, setForm] = useState(initialForm);
  const [errors, setErrors] = useState({});
  const [prereqWarning, setPrereqWarning] = useState(null);
  const [touched, setTouched] = useState({});

  const handleChange = useCallback((field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    setTouched((prev) => ({ ...prev, [field]: true }));
    if (errors[field]) {
      setErrors((prev) => {
        const next = { ...prev };
        delete next[field];
        return next;
      });
    }
  }, [errors]);

  const handleBlur = useCallback((field) => {
    setTouched((prev) => ({ ...prev, [field]: true }));
    const validation = validateCourseForm({ ...form, [field]: form[field] });
    if (!validation.isValid && validation.errors[field]) {
      setErrors((prev) => ({ ...prev, [field]: validation.errors[field] }));
    }
  }, [form]);

  const validate = useCallback(() => {
    const validation = validateCourseForm(form);
    setErrors(validation.errors);

    if (editCourse && form.prerequisites?.length > 0) {
      const cycleResult = findCoursePrerequisiteCycle(editCourse.id, allCourses);
      if (cycleResult?.inCycle) {
        setPrereqWarning(cycleResult);
        setErrors((prev) => ({ ...prev, prerequisites: t("courses.prerequisiteCycleDetected", { cycle: cycleResult.cycle.join(" \u2192 ") }) }));
        return false;
      }
    }
    setPrereqWarning(null);
    return validation.isValid;
  }, [form, editCourse, allCourses, t]);

  const setPrerequisites = useCallback((prereqIds) => {
    setForm((prev) => ({ ...prev, prerequisites: prereqIds }));
    setTouched((prev) => ({ ...prev, prerequisites: true }));
  }, []);

  const reset = useCallback((newForm = EMPTY_FORM) => {
    setForm(newForm);
    setErrors({});
    setPrereqWarning(null);
    setTouched({});
  }, []);

  const sanitizedForm = useMemo(() => sanitizeCourseForm(form), [form]);

  const fieldError = (field) => touched[field] ? errors[field] : null;
  const hasError = (field) => !!fieldError(field);

  return {
    form,
    errors,
    touched,
    prereqWarning,
    handleChange,
    handleBlur,
    validate,
    setPrerequisites,
    reset,
    sanitizedForm,
    fieldError,
    hasError,
    setForm,
    setErrors,
  };
}