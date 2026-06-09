import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  ClipboardList, Plus, Trash2, X, AlertTriangle, RefreshCw, BookOpen,
  Edit2, Calendar, Search, Lock, Unlock, ChevronLeft, ChevronRight,
  Grid3X3, Table2,
} from "lucide-react";
import * as academicPlanService from "../../../core/services/academicPlanService";
import * as courseService from "../../../core/services/courseService";
import * as structureService from "../../../core/services/structureService";
import PermissionGate from "../../../core/auth/PermissionGate";
import { useToast } from "../../../core/components/Toast";
import { SkeletonTable } from "../../../core/components/Skeleton";
import EmptyState from "../../../core/components/EmptyState";
import BulkActionBar from "../../../core/components/BulkActionBar";
import CurriculumGrid from "../components/CurriculumGrid";
import "../styles/academicPlans.css";

const PAGE_SIZE = 20;

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
  const { addToast } = useToast();
  const [structureNodes, setStructureNodes] = useState([]);
  const [structureLoading, setStructureLoading] = useState(false);
  const [selectedStructureId, setSelectedStructureId] = useState("");

  const [plans, setPlans] = useState([]);
  const [plansLoading, setPlansLoading] = useState(false);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [refreshKey, setRefreshKey] = useState(0);

  const [selectedPlan, setSelectedPlan] = useState(null);
  const [planLoading, setPlanLoading] = useState(false);

  const [courses, setCourses] = useState([]);

  const [error, setError] = useState(null);

  const [planModal, setPlanModal] = useState(null);
  const [planForm, setPlanForm] = useState(EMPTY_PLAN_FORM);
  const [planFormError, setPlanFormError] = useState("");
  const [planSaving, setPlanSaving] = useState(false);

  const [courseModal, setCourseModal] = useState(false);
  const [courseForm, setCourseForm] = useState(EMPTY_COURSE_FORM);
  const [courseFormError, setCourseFormError] = useState("");
  const [courseSaving, setCourseSaving] = useState(false);

  const [deletePlan, setDeletePlan] = useState(null);
  const [deletePlanCourse, setDeletePlanCourse] = useState(null);

  const [selectedIds, setSelectedIds] = useState(new Set());
  const [lifecycleLoading, setLifecycleLoading] = useState(null);
  const [curriculumView, setCurriculumView] = useState("grid");

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

  const loadPlans = useCallback(async (p = 1) => {
    if (!selectedStructureId) {
      setPlans([]);
      return;
    }
    setPlansLoading(true);
    setError(null);
    try {
      const params = {
        Page: p,
        PageSize: PAGE_SIZE,
        StructureNodeId: selectedStructureId,
        Search: search.trim() || undefined,
      };
      const result = await academicPlanService.searchAcademicPlans(params);
      setPlans(Array.isArray(result?.items) ? result.items : []);
      setTotalCount(result?.totalCount || 0);
      setPage(p);
    } catch (err) {
      setError(err.message || "Failed to load plans");
      setPlans([]);
    } finally {
      setPlansLoading(false);
    }
  }, [selectedStructureId, search]);

  useEffect(() => {
    loadPlans(1);
    setSelectedPlan(null);
    setSelectedIds(new Set());
  }, [refreshKey, selectedStructureId]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const handleSearch = () => {
    setPage(1);
    setSelectedIds(new Set());
    loadPlans(1);
  };

  const handleSearchKeyDown = (e) => {
    if (e.key === "Enter") handleSearch();
  };

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

  const allSelected = plans.length > 0 && plans.every(p => selectedIds.has(p.id));
  const someSelected = plans.some(p => selectedIds.has(p.id));

  const handleSelectAll = () => {
    if (allSelected) setSelectedIds(new Set());
    else setSelectedIds(new Set(plans.map(p => p.id)));
  };

  const handleSelectOne = (id, e) => {
    if (e) e.stopPropagation();
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const handleClose = async (plan) => {
    setLifecycleLoading(plan.id);
    try {
      await academicPlanService.closeAcademicPlan(plan.id);
      addToast(`${plan.name} closed`, "success");
      setRefreshKey(k => k + 1);
    } catch (err) {
      addToast(err.message || "Failed to close plan", "error");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const handleOpen = async (plan) => {
    setLifecycleLoading(plan.id);
    try {
      await academicPlanService.openAcademicPlan(plan.id);
      addToast(`${plan.name} reopened`, "success");
      setRefreshKey(k => k + 1);
    } catch (err) {
      addToast(err.message || "Failed to reopen plan", "error");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const handleBulkDelete = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length) return;
    try {
      await academicPlanService.bulkDeleteAcademicPlans(ids);
      addToast(`${ids.length} plan(s) deleted`, "success");
      setSelectedIds(new Set());
      setRefreshKey(k => k + 1);
    } catch (err) {
      addToast(`Bulk delete failed: ${err.message}`, "error");
    }
  };

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
        addToast("Plan created", "success");
      } else if (planModal === "edit" && selectedPlan) {
        await academicPlanService.updateAcademicPlan(selectedPlan.id, {
          name: planForm.name.trim(),
          effectiveFrom: planForm.effectiveFrom || null,
          effectiveTo: planForm.effectiveTo || null,
          isActive: planForm.isActive,
        });
        addToast("Plan updated", "success");
      }
      closePlanModal();
      setRefreshKey(k => k + 1);
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
      addToast("Plan deleted", "success");
      if (selectedPlan?.id === deletePlan.id) setSelectedPlan(null);
      setDeletePlan(null);
      setRefreshKey(k => k + 1);
    } catch (err) {
      addToast(err.message || "Failed to delete plan", "error");
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
      addToast("Course added", "success");
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
      addToast("Course removed", "success");
      setDeletePlanCourse(null);
      await reloadSelectedPlan();
    } catch (err) {
      addToast(err.message || "Failed to remove course", "error");
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
          <PermissionGate resource="courses.academic-plans" minLevel={2}>
            <button
              className="aplans-btn aplans-btn-primary"
              onClick={openCreatePlan}
              disabled={!selectedStructureId}
            >
              <Plus size={14} />
              {t("new_plan")}
            </button>
          </PermissionGate>
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
          <button className="aplans-btn aplans-btn-outline" onClick={() => setRefreshKey(k => k + 1)}>
            <RefreshCw size={12} />
            {t("refresh")}
          </button>
        )}
      </div>

      <div className="aplans-grid">
        <div className="aplans-list-card">
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 10 }}>
            <h3 style={{ margin: 0 }}><ClipboardList size={15} /> {t("plans")}</h3>
            {plans.length > 0 && (
              <input type="checkbox"
                checked={allSelected}
                ref={el => { if (el) el.indeterminate = someSelected && !allSelected; }}
                onChange={handleSelectAll}
                style={{ marginRight: 4 }}
              />
            )}
          </div>

          <div className="aplans-toolbar" style={{ marginBottom: 10 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 6, background: "white", border: "1px solid #e5e7eb", borderRadius: 8, padding: "5px 10px", flex: 1 }}>
              <Search size={13} style={{ color: "#6b7280" }} />
              <input
                type="text"
                placeholder="Search plans…"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                onKeyDown={handleSearchKeyDown}
                style={{ border: "none", outline: "none", flex: 1, fontSize: 13, fontFamily: "inherit", color: "#1a1f5e" }}
              />
              {search && (
                <button style={{ background: "transparent", border: "none", cursor: "pointer", color: "#6b7280", padding: 2, display: "flex" }}
                  onClick={() => { setSearch(""); setRefreshKey(k => k + 1); }}>
                  <X size={13} />
                </button>
              )}
            </div>
            <button className="aplans-btn aplans-btn-outline" style={{ padding: "5px 12px" }} onClick={handleSearch}>
              <Search size={12} /> Search
            </button>
          </div>

          {plansLoading ? (
            <SkeletonTable rows={5} cols={3} />
          ) : !selectedStructureId ? (
            <div className="aplans-empty" style={{ padding: "32px 16px" }}>
              <ClipboardList size={28} />
              <p style={{ fontSize: 12, color: "#6b7280" }}>{t("pick_structure_node")}</p>
            </div>
          ) : plans.length === 0 ? (
            <EmptyState
              icon={ClipboardList}
              title="No plans found"
              message={totalCount === 0 ? t("create_plan_for_node") : "Try adjusting your search."}
              actionLabel={totalCount === 0 ? t("new_plan") : undefined}
              onAction={totalCount === 0 ? openCreatePlan : undefined}
            />
          ) : (
            <>
              {plans.map((plan) => (
                <div
                  key={plan.id}
                  className={`aplans-plan-row ${selectedPlan?.id === plan.id ? "active" : ""}`}
                  onClick={() => {
                    setSelectedPlan(plan);
                    setPlanLoading(true);
                    academicPlanService.fetchAcademicPlan(plan.id)
                      .then((d) => setSelectedPlan(d))
                      .catch((err) => setError(err.message))
                      .finally(() => setPlanLoading(false));
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <input type="checkbox"
                      checked={selectedIds.has(plan.id)}
                      onChange={(e) => handleSelectOne(plan.id, e)}
                      onClick={(e) => e.stopPropagation()}
                    />
                    <span className="plan-name" style={{ flex: 1 }}>{plan.name}</span>
                    <div style={{ display: "flex", gap: 2, flexShrink: 0 }}>
                      {plan.isClosed ? (
                        <PermissionGate resource="courses.academic-plans" minLevel={4}>
                          <button className="aplans-action-btn edit"
                            onClick={(e) => { e.stopPropagation(); handleOpen(plan); }}
                            disabled={lifecycleLoading === plan.id}
                            title="Reopen"
                            style={{ padding: "3px 5px" }}
                          >
                            <Unlock size={11} />
                          </button>
                        </PermissionGate>
                      ) : (
                        <PermissionGate resource="courses.academic-plans" minLevel={3}>
                          <button className="aplans-action-btn edit"
                            onClick={(e) => { e.stopPropagation(); handleClose(plan); }}
                            disabled={lifecycleLoading === plan.id}
                            title="Close"
                            style={{ padding: "3px 5px" }}
                          >
                            <Lock size={11} />
                          </button>
                        </PermissionGate>
                      )}
                    </div>
                  </div>
                  <div style={{ display: "flex", alignItems: "center", gap: 6, marginTop: 2 }}>
                    <span className="plan-meta" style={{ fontSize: 11 }}>
                      {formatDate(plan.effectiveFrom)} → {plan.effectiveTo ? formatDate(plan.effectiveTo) : t("open")}
                    </span>
                  </div>
                  <div style={{ display: "flex", gap: 4, marginTop: 2 }}>
                    <span className={`aplans-badge ${plan.isActive ? "aplans-badge-active" : "aplans-badge-inactive"}`}>
                      {plan.isActive ? t("active") : t("inactive")}
                    </span>
                    <span className={`aplans-badge ${plan.isClosed ? "aplans-badge-inactive" : "aplans-badge-active"}`}
                      style={{ fontSize: 10 }}>
                      {plan.isClosed ? "Closed" : "Open"}
                    </span>
                  </div>
                </div>
              ))}
              {totalPages > 1 && (
                <div style={{ display: "flex", justifyContent: "center", alignItems: "center", gap: 4, marginTop: 12 }}>
                  <button className="aplans-btn aplans-btn-outline" style={{ padding: "4px 10px", fontSize: 12 }}
                    disabled={page <= 1} onClick={() => loadPlans(page - 1)}>
                    <ChevronLeft size={12} />
                  </button>
                  {Array.from({ length: Math.min(totalPages, 5) }, (_, i) => {
                    let pn;
                    if (totalPages <= 5) pn = i + 1;
                    else {
                      const mid = Math.floor(5 / 2);
                      if (page <= mid + 1) pn = i + 1;
                      else if (page >= totalPages - mid) pn = totalPages - 5 + i + 1;
                      else pn = page - mid + i;
                    }
                    return (
                      <button key={pn} className={`aplans-btn ${pn === page ? "aplans-btn-primary" : "aplans-btn-outline"}`}
                        style={{ minWidth: 32, justifyContent: "center", padding: "4px 8px", fontSize: 12 }}
                        onClick={() => loadPlans(pn)}>
                        {pn}
                      </button>
                    );
                  })}
                  <button className="aplans-btn aplans-btn-outline" style={{ padding: "4px 10px", fontSize: 12 }}
                    disabled={page >= totalPages} onClick={() => loadPlans(page + 1)}>
                    <ChevronRight size={12} />
                  </button>
                </div>
              )}
            </>
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
                  <PermissionGate resource="courses.academic-plans" minLevel={3}>
                    <button className="aplans-btn aplans-btn-outline" onClick={() => openEditPlan(selectedPlan)}>
                      <Edit2 size={13} /> {t("edit")}
                    </button>
                  </PermissionGate>
                  <PermissionGate resource="courses.academic-plans" minLevel={5}>
                    <button className="aplans-btn aplans-btn-danger" onClick={() => setDeletePlan(selectedPlan)}>
                      <Trash2 size={13} /> {t("delete")}
                    </button>
                  </PermissionGate>
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
                  <span>Record</span>
                  <strong>{selectedPlan.isClosed ? "Closed" : "Open"}</strong>
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
                <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
                  <div className="aplans-view-toggle">
                    <button
                      className={curriculumView === "grid" ? "active" : ""}
                      onClick={() => setCurriculumView("grid")}
                      title="Visual curriculum grid"
                    >
                      <Grid3X3 size={13} /> Grid
                    </button>
                    <button
                      className={curriculumView === "table" ? "active" : ""}
                      onClick={() => setCurriculumView("table")}
                      title="Table view"
                    >
                      <Table2 size={13} /> Table
                    </button>
                  </div>
                  <PermissionGate resource="courses.academic-plans" minLevel={2}>
                    <button className="aplans-btn aplans-btn-primary" onClick={openAddCourse}>
                      <Plus size={13} /> {t("add_course")}
                    </button>
                  </PermissionGate>
                </div>
              </div>

              {curriculumView === "grid" ? (
                <CurriculumGrid
                  planCourses={selectedPlan.planCourses}
                  courseCatalog={courses}
                  onAddCourse={(data) => {
                    academicPlanService.addPlanCourse(selectedPlan.id, {
                      courseId: data.courseId,
                      level: data.level,
                      semester: data.semester,
                      isMandatory: data.isMandatory,
                    })
                      .then(() => {
                        addToast("Course added to plan", "success");
                        reloadSelectedPlan();
                      })
                      .catch((err) => addToast(err.message || "Failed to add course", "error"));
                  }}
                  onRemoveCourse={(pc) => {
                    academicPlanService.removePlanCourse(selectedPlan.id, pc.id)
                      .then(() => {
                        addToast("Course removed from plan", "success");
                        reloadSelectedPlan();
                      })
                      .catch((err) => addToast(err.message || "Failed to remove course", "error"));
                  }}
                />
              ) : (
                <>
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
                                <PermissionGate resource="courses.academic-plans" minLevel={5}>
                                  <button
                                    className="aplans-action-btn delete"
                                    onClick={() => setDeletePlanCourse(pc)}
                                    title={t("remove")}
                                  >
                                    <Trash2 size={13} />
                                  </button>
                                </PermissionGate>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  )}
                </>
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

      <BulkActionBar
        selectedCount={selectedIds.size}
        onClear={() => setSelectedIds(new Set())}
        actions={[
          {
            label: "Delete", icon: <Trash2 size={13} />, variant: "danger",
            onClick: handleBulkDelete, requiresPermission: true,
            permissionResource: "courses.academic-plans", permissionLevel: 5,
          },
        ]}
      />
    </div>
  );
}

export default AcademicPlansPage;
