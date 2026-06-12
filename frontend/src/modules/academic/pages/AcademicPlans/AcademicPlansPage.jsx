import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  ClipboardList, Plus, Trash2, X, AlertTriangle, RefreshCw, BookOpen,
  Edit2, Search, Lock, Unlock, Grid3X3, Table2, Globe, ChevronRight, Download,
} from "lucide-react";
import * as courseService from "../../../../core/services/courseService";
import { useDomain } from "../../../../core/contexts/DomainContext";
import { useScopePrograms } from "../../../../core/query/useScopePrograms";
import { getNodeTypeConfig, getNodeTypeLabel } from "../../../../core/constants/nodeTypeRegistry";
import { getLocalized, toLocalizedJson } from "../../../../core/utils/getLocalized";
import PermissionGate from "../../../../core/auth/PermissionGate";
import { useToast } from "../../../../core/components/Toast";
import StatusBadge from "../../../../core/components/StatusBadge";
import ConfirmDialog from "../../../../core/components/ConfirmDialog";
import Drawer from "../../../../core/components/Drawer";
import {
  useAcademicPlans, useAcademicPlan, useCreateAcademicPlan, useUpdateAcademicPlan,
  useDeleteAcademicPlan, useCloseAcademicPlan, useOpenAcademicPlan, useAddPlanCourse,
  useRemovePlanCourse, useBulkDeleteAcademicPlans, useBatchSetPlanCourses,
} from "../../../../core/query/useAcademicPlans";
import { useAllPrerequisitePairs } from "../../../../core/query/useCourses";
import CurriculumGrid from "./CurriculumGrid";
import CurriculumTable from "./CurriculumTable";
import "./academicPlans.css";

const PAGE_SIZE = 20;

const EMPTY_PLAN_FORM = {
  structureNodeId: "",
  name: "",
  nameAr: "",
  effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: "",
  isActive: true,
};

function AcademicPlansPage() {
  const { t, i18n } = useTranslation();
  const { t: ta } = useTranslation("academic");
  const { addToast } = useToast();

  // Scope-driven program resolution (navbar scope → academic programs).
  const { scopeNode } = useDomain();
  const { programs, isLoading: programsLoading, hasScope } = useScopePrograms(scopeNode);
  const [selectedProgramId, setSelectedProgramId] = useState("");

  // Course catalog (for the add panel + code/title lookups).
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

  // Queries
  const queryParams = useMemo(() => ({
    page, pageSize: PAGE_SIZE,
    structureNodeId: selectedProgramId,
    search,
  }), [page, selectedProgramId, search]);

  const { data: planData, isLoading: plansLoading, refetch: refetchPlans } = useAcademicPlans(queryParams);
  const plans = planData?.items || [];
  const totalPages = planData?.totalPages || 1;

  const { data: selectedPlan, isLoading: planLoading } = useAcademicPlan(selectedPlanId);
  const { data: prereqPairs = [] } = useAllPrerequisitePairs();

  // Mutations
  const createPlan = useCreateAcademicPlan();
  const updatePlan = useUpdateAcademicPlan();
  const deletePlan = useDeleteAcademicPlan();
  const closePlan = useCloseAcademicPlan();
  const openPlan = useOpenAcademicPlan();
  const addPlanCourse = useAddPlanCourse();
  const removePlanCourse = useRemovePlanCourse();
  const batchSetCourses = useBatchSetPlanCourses();
  const bulkDeletePlans = useBulkDeleteAcademicPlans();

  // Modal state
  const [planModal, setPlanModal] = useState(null);
  const [planForm, setPlanForm] = useState(EMPTY_PLAN_FORM);
  const [planFormError, setPlanFormError] = useState("");
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deletePlanCourse, setDeletePlanCourse] = useState(null);
  const [confirmAction, setConfirmAction] = useState(null);

  // Load course catalog once.
  useEffect(() => {
    let cancelled = false;
    courseService.fetchActiveCourses()
      .then((all) => { if (!cancelled) setCourses(Array.isArray(all) ? all : []); })
      .catch((err) => { if (!cancelled) setError(err.message || "Failed to load courses"); });
    return () => { cancelled = true; };
  }, []);

  // Auto-select the program when the scope resolves to exactly one (a Program
  // scope, or a container with a single program). Keep a valid selection when
  // the program set changes; force an explicit pick when there are several.
  const programIdsKey = programs.map((p) => p.id).join(",");
  useEffect(() => {
    if (programsLoading) return;
    if (!programs.length) { setSelectedProgramId(""); return; }
    setSelectedProgramId((cur) => {
      if (cur && programs.some((p) => p.id === cur)) return cur;
      if (programs.length === 1) return programs[0].id;
      return "";
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [programIdsKey, programsLoading]);

  // Reset plan selection + paging when the active program changes.
  useEffect(() => {
    setSelectedPlanId(null);
    setSelectedIds(new Set());
    setPage(1);
  }, [selectedProgramId]);

  // Auto-select a plan so a program's curriculum shows immediately (prefer the
  // active plan, else the first).
  const planIdsKey = plans.map((p) => p.id).join(",");
  useEffect(() => {
    if (!plans.length) { setSelectedPlanId(null); return; }
    setSelectedPlanId((cur) => {
      if (cur && plans.some((p) => p.id === cur)) return cur;
      return (plans.find((p) => p.isActive) || plans[0]).id;
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [planIdsKey]);

  const courseById = useMemo(() => {
    const map = {};
    for (const c of courses) map[c.id] = c;
    return map;
  }, [courses]);

  const programName = (p) => getLocalized(p.localizedName || p.name, i18n.language) || p.name;
  const selectedProgram = programs.find((p) => p.id === selectedProgramId) || null;
  const ScopeIcon = scopeNode ? (getNodeTypeConfig(scopeNode.type)?.icon || Globe) : Globe;

  const handleSearch = () => { setPage(1); setSelectedIds(new Set()); };

  // Plan CRUD
  const openCreatePlan = () => {
    if (!selectedProgramId) { setError(t("select_program_first")); return; }
    setPlanModal("create");
    setPlanForm({ ...EMPTY_PLAN_FORM, structureNodeId: selectedProgramId });
    setPlanFormError("");
  };

  const openEditPlan = () => {
    if (!selectedPlan) return;
    setPlanModal("edit");
    setPlanForm({
      structureNodeId: selectedPlan.structureNodeId,
      name: selectedPlan.name,
      nameAr: "",
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
        const res = await createPlan.mutateAsync({
          structureNodeId: planForm.structureNodeId,
          // The API stores plan names as {"ar","en"} localized JSON.
          name: toLocalizedJson(planForm.nameAr, planForm.name),
          effectiveFrom: planForm.effectiveFrom,
          effectiveTo: planForm.effectiveTo || null,
        });
        addToast(t("plan_created"), "success");
        if (res?.id) setSelectedPlanId(res.id);
      } else if (planModal === "edit" && selectedPlan) {
        // Names come back localized — only resend when edited (or an Arabic
        // name was provided) to avoid clobbering the other language.
        const payload = {
          id: selectedPlan.id,
          effectiveFrom: planForm.effectiveFrom || null,
          effectiveTo: planForm.effectiveTo || null,
          isActive: planForm.isActive,
        };
        if (planForm.name.trim() !== (selectedPlan.name || "") || planForm.nameAr.trim()) {
          payload.name = toLocalizedJson(planForm.nameAr, planForm.name);
        }
        await updatePlan.mutateAsync(payload);
        addToast(t("plan_updated"), "success");
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
      addToast(t("plan_deleted"), "success");
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
      addToast(t("plan_closed"), "success");
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
      addToast(t("plan_reopened"), "success");
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
      addToast(t("course_added"), "success");
    } catch (err) {
      addToast(err.message || "Failed to add course", "error");
    }
  };

  // Move a course between cells / flip mandatory. There is no dedicated update
  // endpoint, so this is an atomic remove-then-re-add via the batch endpoint.
  const handleUpdateCourse = async (pc, changes) => {
    if (!selectedPlanId) return;
    const next = {
      courseId: pc.courseId,
      level: changes.level ?? pc.level,
      semester: changes.semester ?? pc.semester,
      isMandatory: changes.isMandatory ?? pc.isMandatory,
    };
    if (next.level === pc.level && next.semester === pc.semester && next.isMandatory === pc.isMandatory) {
      return; // no-op
    }
    try {
      await batchSetCourses.mutateAsync({
        planId: selectedPlanId,
        courses: { add: [next], remove: [pc.id] },
      });
    } catch (err) {
      addToast(err.message || "Failed to update course", "error");
    }
  };

  const handleRemoveCourse = async () => {
    if (!deletePlanCourse || !selectedPlanId) return;
    try {
      await removePlanCourse.mutateAsync({ planId: selectedPlanId, courseId: deletePlanCourse.id });
      addToast(t("course_removed"), "success");
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
      addToast(t("plans_deleted", { count: ids.length }), "success");
      setSelectedIds(new Set());
    } catch (err) {
      addToast(`Bulk delete failed: ${err.message}`, "error");
    }
  };

  // Serialize the curriculum matrix to CSV and download it.
  const handleExportCsv = () => {
    if (!selectedPlan) return;
    const esc = (v) => `"${String(v ?? "").replace(/"/g, '""')}"`;
    const lines = [
      ["Code", "Title", "Credits", "Level", "Semester", "Type"].join(","),
      ...(selectedPlan.planCourses || [])
        .slice()
        .sort((a, b) => a.level - b.level || a.semester - b.semester)
        .map((pc) => {
          const c = courseById[pc.courseId] || {};
          return [
            esc(c.code), esc(c.title), c.creditHours || 0,
            pc.level, pc.semester, pc.isMandatory ? "Mandatory" : "Elective",
          ].join(",");
        }),
    ];
    const blob = new Blob(["﻿" + lines.join("\n")], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${(selectedPlan.name || "curriculum").replace(/[^\w؀-ۿ-]+/g, "_")}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    addToast(ta("plans.exported"), "success");
  };

  const formatDate = (iso) => {
    if (!iso) return "—";
    try { return new Date(iso).toLocaleDateString(); } catch { return "—"; }
  };

  const planReadOnly = !!selectedPlan?.isClosed;

  return (
    <div className="aplans-page" style={{ padding: 0 }}>
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
            <button className="aplans-btn aplans-btn-primary" onClick={openCreatePlan} disabled={!selectedProgramId}>
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

      {/* Scope context bar — driven by the navbar scope selector */}
      <div className="aplans-scope-bar">
        <div className="aplans-scope-current" title={scopeNode ? `${getNodeTypeLabel(scopeNode.type)} — ${scopeNode.name}` : t("all_scopes")}>
          <ScopeIcon size={16} />
          <span className="aplans-scope-name">
            {scopeNode ? getLocalized(scopeNode.name, i18n.language) : t("all_programs")}
          </span>
          {scopeNode && <span className="aplans-scope-type">{getNodeTypeLabel(scopeNode.type)}</span>}
        </div>

        <ChevronRight size={14} className="aplans-scope-arrow" />

        <div className="aplans-scope-programs">
          {programsLoading ? (
            <span className="aplans-scope-loading"><RefreshCw size={12} className="aplans-spin" /> {t("loading")}…</span>
          ) : programs.length === 0 ? (
            <span className="aplans-scope-empty">
              {hasScope ? t("no_programs_in_scope") : t("pick_scope_hint")}
            </span>
          ) : (
            programs.map((p) => (
              <button
                key={p.id}
                className={`aplans-program-chip ${selectedProgramId === p.id ? "active" : ""}`}
                onClick={() => setSelectedProgramId(p.id)}
                title={programName(p)}
              >
                <BookOpen size={12} /> {programName(p)}
              </button>
            ))
          )}
        </div>

        {selectedProgramId && (
          <button className="aplans-btn aplans-btn-outline aplans-scope-refresh" onClick={() => refetchPlans()}>
            <RefreshCw size={12} /> {t("refresh")}
          </button>
        )}
      </div>

      {hasScope && !programsLoading && programs.length > 1 && !selectedProgramId && (
        <div className="aplans-scope-prompt">
          {t("select_program_within", { scope: getLocalized(scopeNode.name, i18n.language) })}
        </div>
      )}

      <div className="aplans-grid">
        {/* Left panel: plan list */}
        <div className="aplans-list-card">
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 10 }}>
            <h3 style={{ margin: 0 }}><ClipboardList size={15} /> {t("plans")}</h3>
          </div>

          <div className="aplans-toolbar" style={{ marginBottom: 10 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 6, background: "white", border: "1px solid #e5e7eb", borderRadius: 8, padding: "5px 10px", flex: 1 }}>
              <Search size={13} style={{ color: "#6b7280" }} />
              <input type="text" placeholder={t("search_plans")} value={search}
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
              <Search size={12} /> {t("search")}
            </button>
          </div>

          {plansLoading ? (
            <div className="aplans-empty"><RefreshCw size={24} className="aplans-spin" /></div>
          ) : !selectedProgramId ? (
            <div className="aplans-empty" style={{ padding: "32px 16px" }}>
              <ClipboardList size={28} />
              <p style={{ fontSize: 12, color: "#6b7280" }}>
                {programs.length > 1 ? t("pick_program_above") : t("pick_scope_hint")}
              </p>
            </div>
          ) : plans.length === 0 ? (
            <div className="aplans-empty" style={{ padding: "32px 16px" }}>
              <ClipboardList size={28} />
              <p>{t("create_plan_for_program")}</p>
              <PermissionGate resource="courses.academic-plans" minLevel={2}>
                <button className="aplans-btn aplans-btn-primary" onClick={openCreatePlan}>
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
                            disabled={openPlan.isPending} title={t("reopen")} style={{ padding: "3px 5px" }}>
                            <Unlock size={11} />
                          </button>
                        </PermissionGate>
                      ) : (
                        <PermissionGate resource="courses.academic-plans" minLevel={3}>
                          <button className="aplans-action-btn edit" onClick={(e) => { e.stopPropagation(); setConfirmAction({ type: "close", plan }); }}
                            disabled={closePlan.isPending} title={t("close")} style={{ padding: "3px 5px" }}>
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
                    <StatusBadge status={plan.isClosed ? "closed" : "open"} label={plan.isClosed ? t("closed") : t("open")} style={{ fontSize: 10 }} />
                  </div>
                </div>
              ))}
              {totalPages > 1 && (
                <div style={{ display: "flex", justifyContent: "center", gap: 4, marginTop: 12 }}>
                  <button className="aplans-btn aplans-btn-outline" style={{ padding: "4px 10px", fontSize: 12 }}
                    disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                    {t("previous")}
                  </button>
                  <span style={{ fontSize: 12, color: "#6b7280", padding: "4px 8px" }}>
                    {page} / {totalPages}
                  </span>
                  <button className="aplans-btn aplans-btn-outline" style={{ padding: "4px 10px", fontSize: 12 }}
                    disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                    {t("next")}
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
                  <button className="aplans-btn aplans-btn-outline" onClick={handleExportCsv} title={ta("plans.exportCsv")}>
                    <Download size={13} /> {ta("plans.exportCsv")}
                  </button>
                  <PermissionGate resource="courses.academic-plans" minLevel={3}>
                    <button className="aplans-btn aplans-btn-outline" onClick={openEditPlan} disabled={planReadOnly}>
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
                <div><span>{t("record")}</span><strong><StatusBadge status={selectedPlan.isClosed ? "closed" : "open"} label={selectedPlan.isClosed ? t("closed") : t("open")} /></strong></div>
              </div>

              {planReadOnly && (
                <div className="aplans-readonly-note">
                  <Lock size={13} /> {t("plan_closed_readonly")}
                </div>
              )}

              <div className="aplans-section-title" style={{ justifyContent: "space-between" }}>
                <span><BookOpen size={13} /> {t("plan_courses")}</span>
                <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
                  <div className="aplans-view-toggle">
                    <button className={curriculumView === "grid" ? "active" : ""}
                      onClick={() => setCurriculumView("grid")} title={t("grid_view")}>
                      <Grid3X3 size={13} /> {t("grid")}
                    </button>
                    <button className={curriculumView === "table" ? "active" : ""}
                      onClick={() => setCurriculumView("table")} title={t("table_view")}>
                      <Table2 size={13} /> {t("table")}
                    </button>
                  </div>
                </div>
              </div>

              {curriculumView === "grid" ? (
                <CurriculumGrid
                  planCourses={selectedPlan.planCourses}
                  courseCatalog={courses}
                  prereqPairs={prereqPairs}
                  onAddCourse={handleAddCourse}
                  onRemoveCourse={(pc) => setDeletePlanCourse(pc)}
                  onUpdateCourse={handleUpdateCourse}
                  readOnly={planReadOnly}
                />
              ) : (
                <CurriculumTable
                  planCourses={selectedPlan.planCourses}
                  courseCatalog={courses}
                  prereqPairs={prereqPairs}
                  onAddCourse={handleAddCourse}
                  onRemoveCourse={(pc) => setDeletePlanCourse(pc)}
                  onUpdateCourse={handleUpdateCourse}
                  readOnly={planReadOnly}
                />
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

        {planModal === "create" && selectedProgram && (
          <div className="aplans-drawer-context">
            <BookOpen size={13} /> {programName(selectedProgram)}
          </div>
        )}

        <form onSubmit={handlePlanSubmit} style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <div className="form-group">
            <label htmlFor="plan-name">{t("plan_name")}</label>
            <input id="plan-name" type="text" value={planForm.name}
              onChange={(e) => setPlanForm((p) => ({ ...p, name: e.target.value }))}
              placeholder={t("plan_name_placeholder")} autoFocus
              style={{ width: "100%", padding: "8px 12px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, fontFamily: "inherit" }} />
          </div>
          <div className="form-group">
            <label htmlFor="plan-name-ar">{t("name_arabic")}</label>
            <input id="plan-name-ar" type="text" dir="rtl" value={planForm.nameAr}
              onChange={(e) => setPlanForm((p) => ({ ...p, nameAr: e.target.value }))}
              placeholder="مثال: خطة ٢٠٢٤"
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
        message={t("remove_course_named", { code: courseById[deletePlanCourse?.courseId]?.code || t("this_course") })}
        detail={t("cannot_undo")}
        confirmLabel={t("confirm")}
        variant="danger"
      />

      {/* Close/Reopen Confirmation */}
      <ConfirmDialog
        open={!!confirmAction}
        onClose={() => setConfirmAction(null)}
        onConfirm={confirmAction?.type === "close" ? handleClose : handleOpen}
        title={confirmAction?.type === "close" ? t("close_plan_record") : t("reopen_plan_record")}
        message={`${confirmAction?.type === "close" ? t("close") : t("reopen")} ${confirmAction?.plan?.name}?`}
        detail={confirmAction?.type === "close" ? t("closed_plan_uneditable") : t("reopened_plan_editable")}
        confirmLabel={confirmAction?.type === "close" ? t("yes_close") : t("yes_reopen")}
        variant={confirmAction?.type === "close" ? "warning" : "default"}
        loading={closePlan.isPending || openPlan.isPending}
      />

      {/* Bulk actions */}
      {selectedIds.size > 0 && (
        <div className="bulk-bar">
          <span>{t("selected_count", { count: selectedIds.size })}</span>
          <button onClick={() => setSelectedIds(new Set())}>{t("clear")}</button>
          <PermissionGate resource="courses.academic-plans" minLevel={5}>
            <button className="bulk-danger" onClick={handleBulkDelete} disabled={bulkDeletePlans.isPending}>
              <Trash2 size={13} /> {t("delete_selected")}
            </button>
          </PermissionGate>
        </div>
      )}
    </div>
  );
}

export default AcademicPlansPage;
