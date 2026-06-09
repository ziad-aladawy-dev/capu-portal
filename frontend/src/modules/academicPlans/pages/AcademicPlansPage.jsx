import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  ClipboardList, Plus, Trash2, X, AlertTriangle, RefreshCw, BookOpen,
  Edit2, Search, Lock, Unlock, Grid3X3, Table2, Save,
} from "lucide-react";
import * as academicPlanService from "../../../core/services/academicPlanService";
import * as courseService from "../../../core/services/courseService";
import * as structureService from "../../../core/services/structureService";
import { useDomain } from "../../../core/contexts/DomainContext";
import PermissionGate from "../../../core/auth/PermissionGate";
import { useToast } from "../../../core/components/Toast";
import StatusBadge from "../../../core/components/StatusBadge";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import DataTable from "../../../core/components/DataTable";
import Drawer from "../../../core/components/Drawer";
import { useAcademicPlans, useAcademicPlan, useCreateAcademicPlan, useUpdateAcademicPlan, useDeleteAcademicPlan, useCloseAcademicPlan, useOpenAcademicPlan, useAddPlanCourse, useRemovePlanCourse, useBulkDeleteAcademicPlans } from "../../../core/query/useAcademicPlans";
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

function AcademicPlansPage() {
  const { t } = useTranslation();
  const { addToast } = useToast();

  // Structure + courses lookup data
  const [structureNodes, setStructureNodes] = useState([]);
  const [structureLoading, setStructureLoading] = useState(false);
  const { scopeNode } = useDomain();
  const selectedStructureId = scopeNode?.id || "";
  const [courses, setCourses] = useState([]);
  const [error, setError] = useState(null);

  // Search & pagination
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [selectedIds, setSelectedIds] = useState(new Set());

  // Selected plan detail
  const [selectedPlanId, setSelectedPlanId] = useState(null);

  // View mode
  const [curriculumView, setCurriculumView] = useState("grid");
  const [compact, setCompact] = useState(false);

  // Queries
  const queryParams = useMemo(() => ({
    page, pageSize: PAGE_SIZE,
    structureNodeId: selectedStructureId,
    search,
  }), [page, selectedStructureId, search]);

  const { data: planData, isLoading: plansLoading, refetch: refetchPlans } = useAcademicPlans(queryParams);
  const plans = planData?.items || [];
  const totalPages = planData?.totalPages || 1;

  const { data: selectedPlan, isLoading: planLoading } = useAcademicPlan(selectedPlanId);

  // Mutations
  const createPlan = useCreateAcademicPlan();
  const updatePlan = useUpdateAcademicPlan();
  const deletePlan = useDeleteAcademicPlan();
  const closePlan = useCloseAcademicPlan();
  const openPlan = useOpenAcademicPlan();
  const addPlanCourse = useAddPlanCourse();
  const removePlanCourse = useRemovePlanCourse();
  const bulkDeletePlans = useBulkDeleteAcademicPlans();

  // Modal state
  const [planModal, setPlanModal] = useState(null);
  const [planForm, setPlanForm] = useState(EMPTY_PLAN_FORM);
  const [planFormError, setPlanFormError] = useState("");
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deletePlanCourse, setDeletePlanCourse] = useState(null);
  const [confirmAction, setConfirmAction] = useState(null);

  // Load lookup data
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

  // Reset selection when structure changes
  useEffect(() => {
    setSelectedPlanId(null);
    setSelectedIds(new Set());
  }, [selectedStructureId]);

  const courseById = useMemo(() => {
    const map = {};
    for (const c of courses) map[c.id] = c;
    return map;
  }, [courses]);

  // Credit summary
  const planCreditSummary = useMemo(() => {
    if (!selectedPlan?.planCourses) return null;
    const pcs = selectedPlan.planCourses;
    const byLevel = {};
    let total = 0;
    for (const pc of pcs) {
      const course = courseById[pc.courseId];
      const credits = course?.creditHours || 0;
      if (!byLevel[pc.level]) byLevel[pc.level] = { count: 0, credits: 0 };
      byLevel[pc.level].count++;
      byLevel[pc.level].credits += credits;
      total += credits;
    }
    return { byLevel, total, courseCount: pcs.length };
  }, [selectedPlan, courseById]);

  const handleSearch = () => { setPage(1); setSelectedIds(new Set()); };

  // Plan CRUD
  const openCreatePlan = () => {
    if (!selectedStructureId) {
      setError(t("select_node_first"));
      return;
    }
    setPlanModal("create");
    setPlanForm({ ...EMPTY_PLAN_FORM, structureNodeId: selectedStructureId });
    setPlanFormError("");
  };

  const openEditPlan = () => {
    if (!selectedPlan) return;
    setPlanModal("edit");
    setPlanForm({
      structureNodeId: selectedPlan.structureNodeId,
      name: selectedPlan.name,
      effectiveFrom: selectedPlan.effectiveFrom ? selectedPlan.effectiveFrom.slice(0, 10) : "",
      effectiveTo: selectedPlan.effectiveTo ? selectedPlan.effectiveTo.slice(0, 10) : "",
      isActive: selectedPlan.isActive,
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
    if (!planForm.name.trim()) { setPlanFormError(t("plan_name_required")); return; }
    if (!planForm.effectiveFrom) { setPlanFormError(t("effective_from_required")); return; }
    try {
      if (planModal === "create") {
        await createPlan.mutateAsync({
          structureNodeId: planForm.structureNodeId,
          name: planForm.name.trim(),
          effectiveFrom: planForm.effectiveFrom,
          effectiveTo: planForm.effectiveTo || null,
        });
        addToast("Plan created", "success");
      } else if (planModal === "edit" && selectedPlan) {
        await updatePlan.mutateAsync({
          id: selectedPlan.id,
          name: planForm.name.trim(),
          effectiveFrom: planForm.effectiveFrom || null,
          effectiveTo: planForm.effectiveTo || null,
          isActive: planForm.isActive,
        });
        addToast("Plan updated", "success");
      }
      closePlanModal();
    } catch (err) {
      setPlanFormError(err.message || "Failed to save plan");
    }
  };

  const handleDeletePlan = async () => {
    if (!deleteTarget) return;
    try {
      await deletePlan.mutateAsync(deleteTarget.id);
      addToast("Plan deleted", "success");
      if (selectedPlanId === deleteTarget.id) setSelectedPlanId(null);
      setDeleteTarget(null);
    } catch (err) {
      addToast(err.message || "Failed to delete plan", "error");
      setDeleteTarget(null);
    }
  };

  const handleClose = async () => {
    if (!confirmAction?.plan) return;
    try {
      await closePlan.mutateAsync(confirmAction.plan.id);
      addToast("Plan closed", "success");
      setConfirmAction(null);
    } catch (err) {
      addToast(err.message || "Failed to close plan", "error");
      setConfirmAction(null);
    }
  };

  const handleOpen = async () => {
    if (!confirmAction?.plan) return;
    try {
      await openPlan.mutateAsync(confirmAction.plan.id);
      addToast("Plan reopened", "success");
      setConfirmAction(null);
    } catch (err) {
      addToast(err.message || "Failed to reopen plan", "error");
      setConfirmAction(null);
    }
  };

  const handleAddCourse = async (data) => {
    if (!selectedPlanId) return;
    try {
      await addPlanCourse.mutateAsync({ planId: selectedPlanId, ...data });
      addToast("Course added to plan", "success");
    } catch (err) {
      addToast(err.message || "Failed to add course", "error");
    }
  };

  const handleRemoveCourse = async () => {
    if (!deletePlanCourse || !selectedPlanId) return;
    try {
      await removePlanCourse.mutateAsync({ planId: selectedPlanId, courseId: deletePlanCourse.id });
      addToast("Course removed from plan", "success");
      setDeletePlanCourse(null);
    } catch (err) {
      addToast(err.message || "Failed to remove course", "error");
      setDeletePlanCourse(null);
    }
  };

  const handleBulkDelete = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length) return;
    try {
      await bulkDeletePlans.mutateAsync(ids);
      addToast(`${ids.length} plan(s) deleted`, "success");
      setSelectedIds(new Set());
    } catch (err) {
      addToast(`Bulk delete failed: ${err.message}`, "error");
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
            <button className="aplans-btn aplans-btn-primary" onClick={openCreatePlan} disabled={!selectedStructureId} title={!selectedStructureId ? "Select a specific node to create a plan" : ""}>
              <Plus size={14} /> {t("new_plan")}
            </button>
          </PermissionGate>
        </div>
      </div>

      {error && (
        <div className="aplans-error-banner" role="alert">
          <AlertTriangle size={16} />
          <span>{error}</span>
          <button style={{ marginLeft: "auto", background: "transparent", border: "none", cursor: "pointer", color: "#b91c1c" }}
            onClick={() => setError(null)}>
            <X size={14} />
          </button>
        </div>
      )}

      <div className="aplans-toolbar">
        <span className="label">
          {scopeNode ? `Scope: ${scopeNode.name}` : "All Nodes (Global Scope)"}
        </span>
        <button className="aplans-btn aplans-btn-outline" onClick={() => refetchPlans()}>
          <RefreshCw size={12} /> {t("refresh")}
        </button>
      </div>

      <div className="aplans-grid">
        {/* Left panel: plan list */}
        <div className="aplans-list-card">
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 10 }}>
            <h3 style={{ margin: 0 }}><ClipboardList size={15} /> {t("plans")}</h3>
          </div>

          <div className="aplans-toolbar" style={{ marginBottom: 10 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 6, background: "white", border: "1px solid #e5e7eb", borderRadius: 8, padding: "5px 10px", flex: 1 }}>
              <Search size={13} style={{ color: "#6b7280" }} />
              <input type="text" placeholder="Search plans…" value={search}
                onChange={(e) => setSearch(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && handleSearch()}
                style={{ border: "none", outline: "none", flex: 1, fontSize: 13, fontFamily: "inherit", color: "#1a1f5e" }} />
              {search && (
                <button style={{ background: "transparent", border: "none", cursor: "pointer", color: "#6b7280", padding: 2, display: "flex" }}
                  onClick={() => { setSearch(""); setPage(1); }}>
                  <X size={13} />
                </button>
              )}
            </div>
            <button className="aplans-btn aplans-btn-outline" style={{ padding: "5px 12px" }} onClick={handleSearch}>
              <Search size={12} /> Search
            </button>
          </div>

          {plansLoading ? (
            <div className="aplans-empty"><RefreshCw size={24} style={{ animation: "spin 1s linear infinite" }} /></div>
          ) : plans.length === 0 ? (
            <div className="aplans-empty" style={{ padding: "32px 16px" }}>
              <ClipboardList size={28} />
              <p>{t("create_plan_for_node")}</p>
              <PermissionGate resource="courses.academic-plans" minLevel={2}>
                <button className="aplans-btn aplans-btn-primary" onClick={openCreatePlan} disabled={!selectedStructureId} title={!selectedStructureId ? "Select a specific node to create a plan" : ""}>
                  <Plus size={14} /> {t("new_plan")}
                </button>
              </PermissionGate>
            </div>
          ) : (
            <>
              {plans.map((plan) => (
                <div key={plan.id}
                  className={`aplans-plan-row ${selectedPlanId === plan.id ? "active" : ""}`}
                  onClick={() => setSelectedPlanId(plan.id)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(e) => e.key === "Enter" && setSelectedPlanId(plan.id)}
                  aria-selected={selectedPlanId === plan.id}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <input type="checkbox" checked={selectedIds.has(plan.id)}
                      onChange={(e) => {
                        e.stopPropagation();
                        setSelectedIds((prev) => { const n = new Set(prev); if (n.has(plan.id)) n.delete(plan.id); else n.add(plan.id); return n; });
                      }}
                      onClick={(e) => e.stopPropagation()}
                      aria-label={`Select ${plan.name}`} />
                    <span className="plan-name" style={{ flex: 1 }}>{plan.name}</span>
                    <div style={{ display: "flex", gap: 2, flexShrink: 0 }}>
                      {plan.isClosed ? (
                        <PermissionGate resource="courses.academic-plans" minLevel={4}>
                          <button className="aplans-action-btn edit" onClick={(e) => { e.stopPropagation(); setConfirmAction({ type: "open", plan }); }}
                            disabled={openPlan.isPending} title="Reopen" style={{ padding: "3px 5px" }}>
                            <Unlock size={11} />
                          </button>
                        </PermissionGate>
                      ) : (
                        <PermissionGate resource="courses.academic-plans" minLevel={3}>
                          <button className="aplans-action-btn edit" onClick={(e) => { e.stopPropagation(); setConfirmAction({ type: "close", plan }); }}
                            disabled={closePlan.isPending} title="Close" style={{ padding: "3px 5px" }}>
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
                    <StatusBadge status={plan.isActive ? "active" : "inactive"} label={plan.isActive ? t("active") : t("inactive")} style={{ fontSize: 10 }} />
                    <StatusBadge status={plan.isClosed ? "closed" : "open"} label={plan.isClosed ? "Closed" : "Open"} style={{ fontSize: 10 }} />
                  </div>
                </div>
              ))}
              {totalPages > 1 && (
                <div style={{ display: "flex", justifyContent: "center", gap: 4, marginTop: 12 }}>
                  <button className="aplans-btn aplans-btn-outline" style={{ padding: "4px 10px", fontSize: 12 }}
                    disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                    Previous
                  </button>
                  <span style={{ fontSize: 12, color: "#6b7280", padding: "4px 8px" }}>
                    {page} / {totalPages}
                  </span>
                  <button className="aplans-btn aplans-btn-outline" style={{ padding: "4px 10px", fontSize: 12 }}
                    disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                    Next
                  </button>
                </div>
              )}
            </>
          )}
        </div>

        {/* Right panel: plan detail */}
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
                  <h3 style={{ margin: 0 }}>{selectedPlan.name}</h3>
                </div>
                <div className="aplans-detail-actions">
                  <PermissionGate resource="courses.academic-plans" minLevel={3}>
                    <button className="aplans-btn aplans-btn-outline" onClick={openEditPlan}>
                      <Edit2 size={13} /> {t("edit")}
                    </button>
                  </PermissionGate>
                  <PermissionGate resource="courses.academic-plans" minLevel={5}>
                    <button className="aplans-btn aplans-btn-danger" onClick={() => setDeleteTarget(selectedPlan)}>
                      <Trash2 size={13} /> {t("delete")}
                    </button>
                  </PermissionGate>
                </div>
              </div>

              <div className="aplans-detail-meta">
                <div><span>{t("effective_from")}</span><strong>{formatDate(selectedPlan.effectiveFrom)}</strong></div>
                <div><span>{t("effective_to")}</span><strong>{selectedPlan.effectiveTo ? formatDate(selectedPlan.effectiveTo) : t("open")}</strong></div>
                <div><span>{t("status")}</span><strong><StatusBadge status={selectedPlan.isActive ? "active" : "inactive"} label={selectedPlan.isActive ? t("active") : t("inactive")} /></strong></div>
                <div><span>Record</span><strong><StatusBadge status={selectedPlan.isClosed ? "closed" : "open"} label={selectedPlan.isClosed ? "Closed" : "Open"} /></strong></div>
              </div>

              <div className="aplans-section-title" style={{ justifyContent: "space-between" }}>
                <span><BookOpen size={13} /> {t("plan_courses")}</span>
                <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
                  <div className="aplans-view-toggle">
                    <button className={curriculumView === "grid" ? "active" : ""}
                      onClick={() => setCurriculumView("grid")} title="Visual curriculum grid">
                      <Grid3X3 size={13} /> Grid
                    </button>
                    <button className={curriculumView === "table" ? "active" : ""}
                      onClick={() => setCurriculumView("table")} title="Table view">
                      <Table2 size={13} /> Table
                    </button>
                  </div>
                </div>
              </div>

              {curriculumView === "grid" ? (
                <CurriculumGrid
                  planCourses={selectedPlan.planCourses}
                  courseCatalog={courses}
                  onAddCourse={handleAddCourse}
                  onRemoveCourse={(pc) => setDeletePlanCourse(pc)}
                />
              ) : (
                <>
                  <DataTable
                    columns={[
                      { key: "code", label: t("code"), render: (_, row) => {
                        const course = courseById[row.courseId];
                        return <strong style={{ fontFamily: "Space Mono, monospace" }}>{course?.code || "—"}</strong>;
                      }},
                      { key: "title", label: t("title"), render: (_, row) => courseById[row.courseId]?.title || "—" },
                      { key: "credits", label: "Credits", render: (_, row) => courseById[row.courseId]?.creditHours || "—", align: "center" },
                      { key: "level", label: t("level"), align: "center" },
                      { key: "semester", label: t("semester"), align: "center" },
                      { key: "isMandatory", label: t("type"), render: (v) => (
                        <StatusBadge status={v ? "active" : "draft"} label={v ? t("mandatory") : t("elective")} />
                      )},
                      { key: "actions", label: "", render: (_, row) => (
                        <PermissionGate resource="courses.academic-plans" minLevel={5}>
                          <button className="aplans-action-btn delete" onClick={(e) => { e.stopPropagation(); setDeletePlanCourse(row); }} title={t("remove")}>
                            <Trash2 size={13} />
                          </button>
                        </PermissionGate>
                      ), nowrap: true },
                    ]}
                    data={selectedPlan.planCourses || []}
                    emptyIcon={BookOpen}
                    emptyTitle={t("no_courses_plan")}
                    emptyMessage="Click a cell in Grid view to add courses."
                    compact={compact}
                    onCompactToggle={() => setCompact((c) => !c)}
                  />
                  {planCreditSummary && (
                    <div style={{ marginTop: 12, padding: "10px 16px", background: "#f8f9fc", borderRadius: 8, border: "1px solid #e5e7eb", display: "flex", flexWrap: "wrap", gap: 16, alignItems: "center", fontSize: 13 }}>
                      <span><strong>{planCreditSummary.courseCount}</strong> courses</span>
                      <span><strong>{planCreditSummary.total}</strong> total credits</span>
                      {Object.entries(planCreditSummary.byLevel).sort(([a], [b]) => Number(a) - Number(b)).map(([level, info]) => (
                        <span key={level} style={{ color: "#6b7280" }}>
                          Level {level}: <strong>{info.credits}</strong> cr ({info.count} courses)
                        </span>
                      ))}
                    </div>
                  )}
                </>
              )}
            </>
          )}
        </div>
      </div>

      {/* Plan Create/Edit Drawer */}
      <Drawer
        open={!!planModal}
        onClose={closePlanModal}
        title={planModal === "create" ? t("new_academic_plan") : t("edit_academic_plan")}
        width={440}
        loading={createPlan.isPending || updatePlan.isPending}
        footer={
          <>
            <button className="btn-cancel" onClick={closePlanModal}>{t("cancel")}</button>
            <button className="btn-primary" onClick={handlePlanSubmit} disabled={createPlan.isPending || updatePlan.isPending}>
              {createPlan.isPending || updatePlan.isPending ? t("saving") : planModal === "create" ? t("create") : t("save")}
            </button>
          </>
        }
      >
        {planFormError && <span className="aplans-form-error" role="alert" style={{ display: "block", marginBottom: 16 }}>{planFormError}</span>}

        <form onSubmit={handlePlanSubmit} style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <div className="form-group">
            <label htmlFor="plan-name">{t("plan_name")}</label>
            <input id="plan-name" type="text" value={planForm.name}
              onChange={(e) => setPlanForm((p) => ({ ...p, name: e.target.value }))}
              placeholder={t("plan_name_placeholder")} autoFocus
              style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }} />
          </div>
          <div style={{ display: "flex", gap: 12 }}>
            <div className="form-group" style={{ flex: 1 }}>
              <label htmlFor="plan-eff-from">{t("effective_from")}</label>
              <input id="plan-eff-from" type="date" value={planForm.effectiveFrom}
                onChange={(e) => setPlanForm((p) => ({ ...p, effectiveFrom: e.target.value }))}
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }} />
            </div>
            <div className="form-group" style={{ flex: 1 }}>
              <label htmlFor="plan-eff-to">{t("effective_to_optional")}</label>
              <input id="plan-eff-to" type="date" value={planForm.effectiveTo}
                onChange={(e) => setPlanForm((p) => ({ ...p, effectiveTo: e.target.value }))}
                style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }} />
            </div>
          </div>
          {planModal === "edit" && (
            <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 13 }}>
              <input type="checkbox" checked={planForm.isActive}
                onChange={(e) => setPlanForm((p) => ({ ...p, isActive: e.target.checked }))}
                style={{ accentColor: "#1a1f5e" }} />
              {t("active")}
            </label>
          )}
        </form>
      </Drawer>

      {/* Delete Plan Confirmation */}
      <ConfirmDialog
        open={!!deleteTarget && !deletePlanCourse}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleDeletePlan}
        title={t("delete_plan_title")}
        message={t("delete_plan_confirm", { name: deleteTarget?.name })}
        detail={t("cannot_undo")}
        confirmLabel={t("confirm")}
        variant="danger"
        loading={deletePlan.isPending}
      />

      {/* Remove Course Confirmation */}
      <ConfirmDialog
        open={!!deletePlanCourse}
        onClose={() => setDeletePlanCourse(null)}
        onConfirm={handleRemoveCourse}
        title={t("remove_course_plan")}
        message={`Remove ${courseById[deletePlanCourse?.courseId]?.code || "this course"} from the plan?`}
        detail={t("cannot_undo")}
        confirmLabel={t("confirm")}
        variant="danger"
      />

      {/* Close/Reopen Confirmation */}
      <ConfirmDialog
        open={!!confirmAction}
        onClose={() => setConfirmAction(null)}
        onConfirm={confirmAction?.type === "close" ? handleClose : handleOpen}
        title={confirmAction?.type === "close" ? "Close Plan Record" : "Reopen Plan Record"}
        message={`${confirmAction?.type === "close" ? "Close" : "Reopen"} ${confirmAction?.plan?.name}?`}
        detail={confirmAction?.type === "close" ? "A closed plan cannot be edited." : "A reopened plan will be editable again."}
        confirmLabel={confirmAction?.type === "close" ? "Yes, Close" : "Yes, Reopen"}
        variant={confirmAction?.type === "close" ? "warning" : "default"}
        loading={closePlan.isPending || openPlan.isPending}
      />

      {/* Bulk actions */}
      {selectedIds.size > 0 && (
        <div className="bulk-bar">
          <span>{selectedIds.size} selected</span>
          <button onClick={() => setSelectedIds(new Set())}>Clear</button>
          <PermissionGate resource="courses.academic-plans" minLevel={5}>
            <button className="bulk-danger" onClick={handleBulkDelete} disabled={bulkDeletePlans.isPending}>
              <Trash2 size={13} /> Delete Selected
            </button>
          </PermissionGate>
        </div>
      )}
    </div>
  );
}

export default AcademicPlansPage;
