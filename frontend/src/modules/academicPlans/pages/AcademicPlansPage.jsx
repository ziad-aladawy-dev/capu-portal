import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  ClipboardList, Plus, Trash2, X, AlertTriangle, RefreshCw, BookOpen,
  Edit2, Calendar,
} from "lucide-react";
import * as academicPlanService from "../../../core/services/academicPlanService";
import * as courseService from "../../../core/services/courseService";
import * as structureService from "../../../core/services/structureService";
import "../styles/academicPlans.css";

const EMPTY_PLAN_FORM = {
  structureNodeId: "",
  name: "",
  effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: "",
  isActive: true,
};

const EMPTY_COURSE_FORM = {
  courseId: "",
  level: 1,
  semester: 1,
  isMandatory: true,
};

function AcademicPlansPage() {
  const { t } = useTranslation();
  const [structureNodes, setStructureNodes] = useState([]);
  const [structureLoading, setStructureLoading] = useState(false);
  const [selectedStructureId, setSelectedStructureId] = useState("");

  const [plans, setPlans] = useState([]);
  const [plansLoading, setPlansLoading] = useState(false);

  const [selectedPlan, setSelectedPlan] = useState(null);
  const [planLoading, setPlanLoading] = useState(false);

  const [courses, setCourses] = useState([]);

  const [error, setError] = useState(null);

  const [planModal, setPlanModal] = useState(null); // 'create' | 'edit'
  const [planForm, setPlanForm] = useState(EMPTY_PLAN_FORM);
  const [planFormError, setPlanFormError] = useState("");
  const [planSaving, setPlanSaving] = useState(false);

  const [courseModal, setCourseModal] = useState(false);
  const [courseForm, setCourseForm] = useState(EMPTY_COURSE_FORM);
  const [courseFormError, setCourseFormError] = useState("");
  const [courseSaving, setCourseSaving] = useState(false);

  const [deletePlan, setDeletePlan] = useState(null);
  const [deletePlanCourse, setDeletePlanCourse] = useState(null);

  useEffect(() => {
    let cancelled = false;
    async function loadLookups() {
      setStructureLoading(true);
      try {
        const [programs, levels, departments, allCourses] = await Promise.all([
          structureService.fetchPrograms(),
          structureService.fetchLevels(),
          structureService.fetchDepartments(),
          courseService.fetchActiveCourses(),
        ]);
        if (cancelled) return;
        const nodes = [
          ...(programs || []).map((n) => ({ ...n, _type: "Program" })),
          ...(levels || []).map((n) => ({ ...n, _type: "Level" })),
          ...(departments || []).map((n) => ({ ...n, _type: "Department" })),
        ];
        setStructureNodes(nodes);
        setCourses(Array.isArray(allCourses) ? allCourses : []);
      } catch (err) {
        if (!cancelled) setError(err.message || "Failed to load lookups");
      } finally {
        if (!cancelled) setStructureLoading(false);
      }
    }
    loadLookups();
    return () => { cancelled = true; };
  }, []);

  const loadPlans = useCallback(async (structureNodeId) => {
    if (!structureNodeId) {
      setPlans([]);
      return;
    }
    setPlansLoading(true);
    setError(null);
    try {
      const data = await academicPlanService.fetchPlansForStructure(structureNodeId);
      setPlans(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || "Failed to load plans");
      setPlans([]);
    } finally {
      setPlansLoading(false);
    }
  }, []);

  useEffect(() => {
    loadPlans(selectedStructureId);
    setSelectedPlan(null);
  }, [selectedStructureId, loadPlans]);

  const reloadSelectedPlan = useCallback(async () => {
    if (!selectedPlan?.id) return;
    setPlanLoading(true);
    try {
      const data = await academicPlanService.fetchAcademicPlan(selectedPlan.id);
      setSelectedPlan(data);
      setPlans((prev) =>
        prev.map((p) => (p.id === data.id ? { ...p, ...data } : p))
      );
    } catch (err) {
      setError(err.message || "Failed to load plan details");
    } finally {
      setPlanLoading(false);
    }
  }, [selectedPlan?.id]);

  const courseById = useMemo(() => {
    const map = {};
    for (const c of courses) map[c.id] = c;
    return map;
  }, [courses]);

  const openCreatePlan = () => {
    if (!selectedStructureId) {
      setError(t("select_node_first"));
      return;
    }
    setPlanModal("create");
    setPlanForm({ ...EMPTY_PLAN_FORM, structureNodeId: selectedStructureId });
    setPlanFormError("");
  };

  const openEditPlan = (plan) => {
    setPlanModal("edit");
    setPlanForm({
      structureNodeId: plan.structureNodeId,
      name: plan.name,
      effectiveFrom: plan.effectiveFrom ? plan.effectiveFrom.slice(0, 10) : "",
      effectiveTo: plan.effectiveTo ? plan.effectiveTo.slice(0, 10) : "",
      isActive: plan.isActive,
    });
    setPlanFormError("");
  };

  const closePlanModal = () => {
    setPlanModal(null);
    setPlanForm(EMPTY_PLAN_FORM);
    setPlanFormError("");
  };

  const handlePlanSubmit = async (e) => {
    e.preventDefault();
    if (!planForm.name.trim()) {
      setPlanFormError(t("plan_name_required"));
      return;
    }
    if (!planForm.effectiveFrom) {
      setPlanFormError(t("effective_from_required"));
      return;
    }
    setPlanSaving(true);
    try {
      if (planModal === "create") {
        await academicPlanService.createAcademicPlan({
          structureNodeId: planForm.structureNodeId,
          name: planForm.name.trim(),
          effectiveFrom: planForm.effectiveFrom,
          effectiveTo: planForm.effectiveTo || null,
        });
      } else if (planModal === "edit" && selectedPlan) {
        await academicPlanService.updateAcademicPlan(selectedPlan.id, {
          name: planForm.name.trim(),
          effectiveFrom: planForm.effectiveFrom || null,
          effectiveTo: planForm.effectiveTo || null,
          isActive: planForm.isActive,
        });
      }
      closePlanModal();
      await loadPlans(selectedStructureId);
      if (planModal === "edit") await reloadSelectedPlan();
    } catch (err) {
      setPlanFormError(err.message || "Failed to save plan");
    } finally {
      setPlanSaving(false);
    }
  };

  const handleDeletePlan = async () => {
    if (!deletePlan) return;
    try {
      await academicPlanService.deleteAcademicPlan(deletePlan.id);
      if (selectedPlan?.id === deletePlan.id) setSelectedPlan(null);
      setDeletePlan(null);
      await loadPlans(selectedStructureId);
    } catch (err) {
      setError(err.message || "Failed to delete plan");
      setDeletePlan(null);
    }
  };

  const openAddCourse = () => {
    setCourseModal(true);
    setCourseForm(EMPTY_COURSE_FORM);
    setCourseFormError("");
  };

  const handleAddCourse = async (e) => {
    e.preventDefault();
    if (!courseForm.courseId) {
      setCourseFormError(t("pick_course"));
      return;
    }
    setCourseSaving(true);
    try {
      await academicPlanService.addPlanCourse(selectedPlan.id, {
        courseId: courseForm.courseId,
        level: Number(courseForm.level),
        semester: Number(courseForm.semester),
        isMandatory: courseForm.isMandatory,
      });
      setCourseModal(false);
      await reloadSelectedPlan();
    } catch (err) {
      setCourseFormError(err.message || "Failed to add course");
    } finally {
      setCourseSaving(false);
    }
  };

  const handleRemoveCourse = async () => {
    if (!deletePlanCourse) return;
    try {
      await academicPlanService.removePlanCourse(selectedPlan.id, deletePlanCourse.id);
      setDeletePlanCourse(null);
      await reloadSelectedPlan();
    } catch (err) {
      setError(err.message || "Failed to remove plan course");
      setDeletePlanCourse(null);
    }
  };

  const formatDate = (iso) => {
    if (!iso) return "—";
    try { return new Date(iso).toLocaleDateString(); } catch { return "—"; }
  };

  return (
    <div className="aplans-page">
      <div className="aplans-header">
        <div className="aplans-header-left">
          <ClipboardList size={22} />
          <div>
            <h1>{t("academic_plans")}</h1>
            <p>{t("compose_curricula")}</p>
          </div>
        </div>
        <div>
          <button
            className="aplans-btn aplans-btn-primary"
            onClick={openCreatePlan}
            disabled={!selectedStructureId}
          >
            <Plus size={14} />
            {t("new_plan")}
          </button>
        </div>
      </div>

      {error && (
        <div className="aplans-error-banner">
          <AlertTriangle size={16} />
          <span>{error}</span>
          <button
            style={{ marginLeft: "auto", background: "transparent", border: "none", cursor: "pointer", color: "#b91c1c" }}
            onClick={() => setError(null)}
          >
            <X size={14} />
          </button>
        </div>
      )}

      <div className="aplans-toolbar">
        <span className="label">{t("structure_node")}</span>
        <select
          value={selectedStructureId}
          onChange={(e) => setSelectedStructureId(e.target.value)}
          disabled={structureLoading}
        >
          <option value="">{t("select_structure_node")}</option>
          {structureNodes.map((n) => (
            <option key={n.id} value={n.id}>
              [{n._type}] {n.name}
            </option>
          ))}
        </select>
        {selectedStructureId && (
          <button className="aplans-btn aplans-btn-outline" onClick={() => loadPlans(selectedStructureId)}>
            <RefreshCw size={12} />
            {t("refresh")}
          </button>
        )}
      </div>

      <div className="aplans-grid">
        <div className="aplans-list-card">
          <h3><ClipboardList size={15} /> {t("plans")}</h3>
          {plansLoading ? (
            <div className="aplans-loading">
              <div className="aplans-spinner" />
              <p>{t("loading_plans")}</p>
            </div>
          ) : !selectedStructureId ? (
            <p style={{ color: "#6b7280", fontSize: 12 }}>
              {t("pick_structure_node")}
            </p>
          ) : plans.length === 0 ? (
            <div className="aplans-empty">
              <ClipboardList size={32} />
              <h3>{t("no_plans_yet")}</h3>
              <p>{t("create_plan_for_node")}</p>
            </div>
          ) : (
            plans.map((plan) => (
              <button
                key={plan.id}
                className={`aplans-plan-row ${selectedPlan?.id === plan.id ? "active" : ""}`}
                onClick={() => {
                  setSelectedPlan(plan);
                  // ensure full details (with PlanCourses) are loaded
                  setPlanLoading(true);
                  academicPlanService.fetchAcademicPlan(plan.id)
                    .then((d) => setSelectedPlan(d))
                    .catch((err) => setError(err.message))
                    .finally(() => setPlanLoading(false));
                }}
                style={{ border: "1px solid #e5e7eb", textAlign: "left", width: "100%", background: "white" }}
              >
                <span className="plan-name">{plan.name}</span>
                <span className="plan-meta">
                  {formatDate(plan.effectiveFrom)} → {plan.effectiveTo ? formatDate(plan.effectiveTo) : t("open")}
                </span>
                <span
                  className={`aplans-badge ${plan.isActive ? "aplans-badge-active" : "aplans-badge-inactive"}`}
                  style={{ marginTop: 4, alignSelf: "flex-start" }}
                >
                  {plan.isActive ? t("active") : t("inactive")}
                </span>
              </button>
            ))
          )}
        </div>

        <div className="aplans-detail-card">
          {!selectedPlan ? (
            <div className="aplans-empty">
              <ClipboardList size={40} />
              <h3>{t("no_plan_selected")}</h3>
              <p>{t("pick_plan_left")}</p>
            </div>
          ) : planLoading ? (
            <div className="aplans-loading">
              <div className="aplans-spinner" />
              <p>{t("loading_plan")}</p>
            </div>
          ) : (
            <>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 8, marginBottom: 12 }}>
                <div>
                  <h3 style={{ margin: 0 }}>
                    <Calendar size={15} />
                    {selectedPlan.name}
                  </h3>
                </div>
                <div className="aplans-detail-actions">
                  <button className="aplans-btn aplans-btn-outline" onClick={() => openEditPlan(selectedPlan)}>
                    <Edit2 size={13} /> {t("edit")}
                  </button>
                  <button className="aplans-btn aplans-btn-danger" onClick={() => setDeletePlan(selectedPlan)}>
                    <Trash2 size={13} /> {t("delete")}
                  </button>
                </div>
              </div>

              <div className="aplans-detail-meta">
                <div>
                  <span>{t("effective_from")}</span>
                  <strong>{formatDate(selectedPlan.effectiveFrom)}</strong>
                </div>
                <div>
                  <span>{t("effective_to")}</span>
                  <strong>{selectedPlan.effectiveTo ? formatDate(selectedPlan.effectiveTo) : t("open")}</strong>
                </div>
                <div>
                  <span>{t("status")}</span>
                  <strong>{selectedPlan.isActive ? t("active") : t("inactive")}</strong>
                </div>
                <div>
                  <span>{t("structure_node")}</span>
                  <strong style={{ fontFamily: "Space Mono, monospace", fontSize: 11 }}>
                    {selectedPlan.structureNodeId}
                  </strong>
                </div>
              </div>

              <div className="aplans-section-title" style={{ justifyContent: "space-between" }}>
                <span><BookOpen size={13} /> {t("plan_courses")}</span>
                <button className="aplans-btn aplans-btn-primary" onClick={openAddCourse}>
                  <Plus size={13} /> {t("add_course")}
                </button>
              </div>

              {!selectedPlan.planCourses || selectedPlan.planCourses.length === 0 ? (
                <div className="aplans-empty" style={{ marginTop: 0 }}>
                  <BookOpen size={32} />
                  <p>{t("no_courses_plan")}</p>
                </div>
              ) : (
                <table className="aplans-table">
                  <thead>
                    <tr>
                      <th>{t("code")}</th>
                      <th>{t("title")}</th>
                      <th>{t("level")}</th>
                      <th>{t("semester")}</th>
                      <th>{t("type")}</th>
                      <th className="col-actions" />
                    </tr>
                  </thead>
                  <tbody>
                    {selectedPlan.planCourses.map((pc) => {
                      const course = courseById[pc.courseId];
                      return (
                        <tr key={pc.id}>
                          <td>
                            <strong style={{ fontFamily: "Space Mono, monospace" }}>
                              {course?.code || pc.courseId.slice(0, 8)}
                            </strong>
                          </td>
                          <td>{course?.title || "—"}</td>
                          <td>{pc.level}</td>
                          <td>{pc.semester}</td>
                          <td>
                            <span className="aplans-badge aplans-badge-active">
                              {pc.isMandatory ? t("mandatory") : t("elective")}
                            </span>
                          </td>
                          <td className="col-actions">
                            <button
                              className="aplans-action-btn delete"
                              onClick={() => setDeletePlanCourse(pc)}
                              title={t("remove")}
                            >
                              <Trash2 size={13} />
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              )}
            </>
          )}
        </div>
      </div>

      {planModal && (
        <div className="aplans-modal-overlay" onClick={closePlanModal}>
          <div className="aplans-modal" onClick={(e) => e.stopPropagation()}>
            <div className="aplans-modal-header">
              <h2>{planModal === "create" ? t("new_academic_plan") : t("edit_academic_plan")}</h2>
              <button className="aplans-modal-close" onClick={closePlanModal}>
                <X size={16} />
              </button>
            </div>
            <form onSubmit={handlePlanSubmit}>
              <div className="aplans-modal-body">
                <div className="aplans-form-group">
                  <label>{t("plan_name")}</label>
                  <input
                    type="text"
                    className="aplans-form-input"
                    value={planForm.name}
                    onChange={(e) => setPlanForm((p) => ({ ...p, name: e.target.value }))}
                    placeholder={t("plan_name_placeholder")}
                    autoFocus
                  />
                </div>
                <div className="aplans-form-row">
                  <div className="aplans-form-group">
                    <label>{t("effective_from")}</label>
                    <input
                      type="date"
                      className="aplans-form-input"
                      value={planForm.effectiveFrom}
                      onChange={(e) => setPlanForm((p) => ({ ...p, effectiveFrom: e.target.value }))}
                    />
                  </div>
                  <div className="aplans-form-group">
                    <label>{t("effective_to_optional")}</label>
                    <input
                      type="date"
                      className="aplans-form-input"
                      value={planForm.effectiveTo}
                      onChange={(e) => setPlanForm((p) => ({ ...p, effectiveTo: e.target.value }))}
                    />
                  </div>
                </div>
                {planModal === "edit" && (
                  <label className="aplans-checkbox-row">
                    <input
                      type="checkbox"
                      checked={planForm.isActive}
                      onChange={(e) => setPlanForm((p) => ({ ...p, isActive: e.target.checked }))}
                    />
                    {t("active")}
                  </label>
                )}
                {planFormError && <span className="aplans-form-error">{planFormError}</span>}
              </div>
              <div className="aplans-modal-footer">
                <button type="button" className="aplans-btn aplans-btn-outline" onClick={closePlanModal}>
                  {t("cancel")}
                </button>
                <button type="submit" className="aplans-btn aplans-btn-primary" disabled={planSaving}>
                  {planSaving ? t("saving") : planModal === "create" ? t("create") : t("save")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {courseModal && (
        <div className="aplans-modal-overlay" onClick={() => setCourseModal(false)}>
          <div className="aplans-modal" onClick={(e) => e.stopPropagation()}>
            <div className="aplans-modal-header">
              <h2>{t("add_course_to_plan")}</h2>
              <button className="aplans-modal-close" onClick={() => setCourseModal(false)}>
                <X size={16} />
              </button>
            </div>
            <form onSubmit={handleAddCourse}>
              <div className="aplans-modal-body">
                <div className="aplans-form-group">
                  <label>{t("course")}</label>
                  <select
                    className="aplans-form-select"
                    value={courseForm.courseId}
                    onChange={(e) => setCourseForm((p) => ({ ...p, courseId: e.target.value }))}
                  >
                    <option value="">{t("select_course")}</option>
                    {courses.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.code} — {c.title}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="aplans-form-row">
                  <div className="aplans-form-group">
                    <label>{t("level")}</label>
                    <input
                      type="number"
                      className="aplans-form-input"
                      min={1}
                      max={10}
                      value={courseForm.level}
                      onChange={(e) => setCourseForm((p) => ({ ...p, level: e.target.value }))}
                    />
                  </div>
                  <div className="aplans-form-group">
                    <label>{t("semester")}</label>
                    <input
                      type="number"
                      className="aplans-form-input"
                      min={1}
                      max={4}
                      value={courseForm.semester}
                      onChange={(e) => setCourseForm((p) => ({ ...p, semester: e.target.value }))}
                    />
                  </div>
                </div>
                <label className="aplans-checkbox-row">
                  <input
                    type="checkbox"
                    checked={courseForm.isMandatory}
                    onChange={(e) => setCourseForm((p) => ({ ...p, isMandatory: e.target.checked }))}
                  />
                  {t("mandatory")}
                </label>
                {courseFormError && <span className="aplans-form-error">{courseFormError}</span>}
              </div>
              <div className="aplans-modal-footer">
                <button type="button" className="aplans-btn aplans-btn-outline" onClick={() => setCourseModal(false)}>
                  {t("cancel")}
                </button>
                <button type="submit" className="aplans-btn aplans-btn-primary" disabled={courseSaving}>
                  {courseSaving ? t("adding") : t("add_course")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {(deletePlan || deletePlanCourse) && (
        <div
          className="aplans-modal-overlay"
          onClick={() => {
            setDeletePlan(null);
            setDeletePlanCourse(null);
          }}
        >
          <div className="aplans-modal" onClick={(e) => e.stopPropagation()}>
            <div className="aplans-modal-header">
              <h2>{deletePlan ? t("delete_plan_title") : t("remove_course_plan")}</h2>
              <button
                className="aplans-modal-close"
                onClick={() => {
                  setDeletePlan(null);
                  setDeletePlanCourse(null);
                }}
              >
                <X size={16} />
              </button>
            </div>
            <div style={{ padding: "20px 22px", textAlign: "center", display: "flex", flexDirection: "column", gap: 10, alignItems: "center" }}>
              <AlertTriangle size={32} color="#dc2626" />
              <p style={{ margin: 0 }}>
                {deletePlan
                  ? t("delete_plan_confirm", { name: deletePlan.name })
                  : t("remove_course_confirm")}
              </p>
              <p style={{ margin: 0, fontSize: 12, color: "#6b7280" }}>
                {t("cannot_undo")}
              </p>
            </div>
            <div className="aplans-modal-footer">
              <button
                className="aplans-btn aplans-btn-outline"
                onClick={() => {
                  setDeletePlan(null);
                  setDeletePlanCourse(null);
                }}
              >
                {t("cancel")}
              </button>
              <button
                className="aplans-btn aplans-btn-danger"
                onClick={deletePlan ? handleDeletePlan : handleRemoveCourse}
              >
                {t("confirm")}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default AcademicPlansPage;
