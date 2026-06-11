import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  BookOpen, Plus, Edit2, Trash2, X, Search, RefreshCw, Lock, Unlock,
} from "lucide-react";
import PermissionGate from "../../../../core/auth/PermissionGate";
import ErrorMessage from "../../../../core/components/ErrorMessage";
import { useToast } from "../../../../core/components/Toast";
import StatusBadge from "../../../../core/components/StatusBadge";
import ConfirmDialog from "../../../../core/components/ConfirmDialog";
import DataTable from "../../../../core/components/DataTable";
import { useAcademic } from "../../../../core/contexts/AcademicContext";
import { useDomain } from "../../../../core/contexts/DomainContext";
import { useCanDo } from "../../../../core/auth/usePermission";
import {
  useCourses, useCreateCourse, useUpdateCourse, useDeleteCourse,
  useCloseCourse, useOpenCourse, useBulkDeleteCourses, useAllPrerequisitePairs,
} from "../../../../core/query/useCourses";
import CourseDetailPanel from "./CourseDetailPanel";
import CourseFormDrawer from "./CourseFormDrawer";
import CatalogHealth from "./CatalogHealth";
import shared from "../../styles/academic.module.css";
import styles from "./CourseCatalog.module.css";

const PAGE_SIZE = 20;

/**
 * Course Catalog — split view. Left: searchable catalog table (CRUD +
 * lifecycle + bulk delete). Right: detail panel with details / prerequisites /
 * offerings tabs (absorbs the old Course Hub drawer).
 */
export default function CourseCatalogPage() {
  const { t } = useTranslation("academic");
  const { addToast } = useToast();
  const { selectedSemesterObj } = useAcademic();
  const { scopeNode } = useDomain();
  const canEdit = useCanDo("courses.courses", 3);

  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [page, setPage] = useState(1);
  const [selectedIds, setSelectedIds] = useState(new Set());
  const [activeCourse, setActiveCourse] = useState(null);

  const queryParams = useMemo(() => ({
    page,
    pageSize: PAGE_SIZE,
    search: appliedSearch || undefined,
    category: categoryFilter,
    isActive: statusFilter === "" ? undefined : statusFilter === "active",
  }), [page, appliedSearch, categoryFilter, statusFilter]);

  const { data: courseData, isLoading, error, refetch } = useCourses(queryParams);
  const courses = courseData?.items || [];
  const totalPages = courseData?.totalPages || 1;
  const totalCount = courseData?.totalCount || 0;

  const { data: prereqPairs = [] } = useAllPrerequisitePairs();
  const prereqCountByCourse = useMemo(() => {
    const m = new Map();
    for (const p of prereqPairs) m.set(p.courseId, (m.get(p.courseId) || 0) + 1);
    return m;
  }, [prereqPairs]);

  const createCourse = useCreateCourse();
  const updateCourse = useUpdateCourse();
  const deleteCourse = useDeleteCourse();
  const closeCourse = useCloseCourse();
  const openCourse = useOpenCourse();
  const bulkDelete = useBulkDeleteCourses();

  const [drawerMode, setDrawerMode] = useState(null);
  const [editTarget, setEditTarget] = useState(null);
  const [serverError, setServerError] = useState("");
  const [confirmAction, setConfirmAction] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);

  const pageStats = useMemo(() => {
    const active = courses.filter((c) => c.isActive).length;
    const closed = courses.filter((c) => c.isClosed).length;
    const credits = courses.reduce((a, c) => a + (c.creditHours || 0), 0);
    return { active, closed, credits };
  }, [courses]);

  // Keep the detail panel in sync when the row it shows is refetched.
  const panelCourse = useMemo(() => {
    if (!activeCourse) return null;
    return courses.find((c) => c.id === activeCourse.id) || activeCourse;
  }, [activeCourse, courses]);

  const applySearch = () => { setAppliedSearch(search); setPage(1); setSelectedIds(new Set()); };
  const clearFilters = () => {
    setSearch(""); setAppliedSearch(""); setCategoryFilter(""); setStatusFilter("");
    setPage(1); setSelectedIds(new Set());
  };

  const handleSubmit = async (payload) => {
    setServerError("");
    try {
      if (drawerMode === "create") {
        await createCourse.mutateAsync(payload);
        addToast(t("courses.courseCreated"), "success");
      } else if (editTarget) {
        await updateCourse.mutateAsync({ id: editTarget.id, ...payload });
        addToast(t("courses.courseUpdated"), "success");
      }
      setDrawerMode(null);
      setEditTarget(null);
    } catch (err) {
      setServerError(err.status === 409 ? t("courses.codeInUse") : err.message || t("courses.saveFailed"));
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteCourse.mutateAsync(deleteTarget.id);
      addToast(t("courses.courseDeleted"), "success");
      if (activeCourse?.id === deleteTarget.id) setActiveCourse(null);
    } catch (err) {
      addToast(err.message || t("courses.saveFailed"), "error");
    } finally {
      setDeleteTarget(null);
    }
  };

  const handleLifecycle = async () => {
    const { type, course } = confirmAction || {};
    if (!course) return;
    try {
      if (type === "close") {
        await closeCourse.mutateAsync(course.id);
        addToast(t("courses.courseClosed", { code: course.code }), "success");
      } else {
        await openCourse.mutateAsync(course.id);
        addToast(t("courses.courseReopened", { code: course.code }), "success");
      }
    } catch (err) {
      addToast(err.message || t("courses.saveFailed"), "error");
    } finally {
      setConfirmAction(null);
    }
  };

  const handleBulkDelete = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length) return;
    try {
      const result = await bulkDelete.mutateAsync(ids);
      const okCount = result?.succeeded ?? ids.length;
      addToast(t("courses.bulkDeleted", { count: okCount }), result?.failed ? "warning" : "success");
      setSelectedIds(new Set());
    } catch (err) {
      addToast(t("courses.bulkDeleteFailed", { message: err.message }), "error");
    }
  };

  const columns = [
    {
      key: "code", label: t("courses.code"), width: 110,
      render: (v) => <span className={shared.codePill}>{v}</span>,
    },
    { key: "title", label: t("courses.courseTitle"), nowrap: false },
    { key: "creditHours", label: t("common.credits"), align: "center", width: 70 },
    {
      key: "category", label: t("courses.category"), width: 150,
      render: (v) => <span style={{ fontSize: 12, color: "#4b5563" }}>{t(`categories.${v}`)}</span>,
    },
    {
      key: "_prereqs", label: t("courses.prerequisites"), align: "center", width: 90,
      render: (_, row) => {
        const n = prereqCountByCourse.get(row.id) || 0;
        return n ? <span className={shared.metaChip}>{n}</span> : <span style={{ color: "#d1d5db" }}>—</span>;
      },
    },
    {
      key: "isActive", label: t("common.status"), width: 90,
      render: (v) => <StatusBadge status={v ? "active" : "inactive"} label={v ? t("common.active") : t("common.inactive")} />,
    },
    {
      key: "actions", label: t("common.actions"), nowrap: true, width: 120,
      render: (_, row) => (
        <div style={{ display: "flex", gap: 4 }} onClick={(e) => e.stopPropagation()}>
          <PermissionGate resource="courses.courses" minLevel={3}>
            <button className={styles.actionBtn} onClick={() => { setEditTarget(row); setDrawerMode("edit"); setServerError(""); }} title={t("common.edit")}>
              <Edit2 size={13} />
            </button>
          </PermissionGate>
          {row.isClosed ? (
            <PermissionGate resource="courses.courses" minLevel={4}>
              <button className={styles.actionBtn} onClick={() => setConfirmAction({ type: "open", course: row })} title={t("courses.reopenRecord")}>
                <Unlock size={13} />
              </button>
            </PermissionGate>
          ) : (
            <PermissionGate resource="courses.courses" minLevel={3}>
              <button className={styles.actionBtn} onClick={() => setConfirmAction({ type: "close", course: row })} title={t("courses.closeRecord")}>
                <Lock size={13} />
              </button>
            </PermissionGate>
          )}
          <PermissionGate resource="courses.courses" minLevel={5}>
            <button className={`${styles.actionBtn} ${styles.actionDanger}`} onClick={() => setDeleteTarget(row)} title={t("common.delete")}>
              <Trash2 size={13} />
            </button>
          </PermissionGate>
        </div>
      ),
    },
  ];

  return (
    <>
      <div className={shared.header}>
        <div className={shared.headerLeft}>
          <BookOpen size={22} />
          <div>
            <h1>{t("courses.title")}</h1>
            <p>{t("courses.subtitle")}</p>
          </div>
        </div>
        <div className={shared.headerActions}>
          <PermissionGate resource="courses.courses" minLevel={2}>
            <button className="btn-primary" onClick={() => { setDrawerMode("create"); setEditTarget(null); setServerError(""); }}>
              <Plus size={14} /> {t("courses.createCourse")}
            </button>
          </PermissionGate>
        </div>
      </div>

      <div className={shared.statsBar}>
        <div className={shared.stat}><span className={shared.statValue}>{totalCount}</span><span className={shared.statLabel}>{t("courses.totalCourses")}</span></div>
        <div className={shared.statDivider} />
        <div className={shared.stat}><span className={shared.statValue} style={{ color: "#166534" }}>{pageStats.active}</span><span className={shared.statLabel}>{t("common.active")} {t("common.perPage")}</span></div>
        <div className={shared.stat}><span className={shared.statValue} style={{ color: "#6b7280" }}>{pageStats.closed}</span><span className={shared.statLabel}>{t("common.closed")} {t("common.perPage")}</span></div>
        <div className={shared.statDivider} />
        <div className={shared.stat}><span className={shared.statValue}>{pageStats.credits}</span><span className={shared.statLabel}>{t("common.credits")} {t("common.perPage")}</span></div>
      </div>

      <CatalogHealth semester={selectedSemesterObj} scopeNodeId={scopeNode?.id} onPickCourse={setActiveCourse} />

      <div className={shared.toolbar}>
        <div className={shared.searchBox}>
          <Search size={14} />
          <input
            type="text"
            placeholder={t("courses.searchPlaceholder")}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && applySearch()}
            aria-label={t("courses.searchPlaceholder")}
          />
          {search && (
            <button style={{ background: "transparent", border: "none", cursor: "pointer", color: "#6b7280", padding: 2, display: "flex" }} onClick={clearFilters}>
              <X size={14} />
            </button>
          )}
        </div>
        <select className={shared.select} value={categoryFilter} onChange={(e) => { setCategoryFilter(e.target.value); setPage(1); }} aria-label={t("courses.category")}>
          <option value="">{t("common.allCategories")}</option>
          {[0, 1, 2, 3, 4, 5].map((v) => <option key={v} value={v}>{t(`categories.${v}`)}</option>)}
        </select>
        <select className={shared.select} value={statusFilter} onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }} aria-label={t("common.status")}>
          <option value="">{t("common.allStatuses")}</option>
          <option value="active">{t("common.activeOnly")}</option>
          <option value="inactive">{t("common.inactiveOnly")}</option>
        </select>
        <button className="btn-primary" onClick={applySearch}><Search size={13} /> {t("common.search")}</button>
        {(appliedSearch || categoryFilter || statusFilter) && (
          <button className="btn-cancel" onClick={clearFilters}><X size={13} /> {t("common.clear")}</button>
        )}
      </div>

      <div className={styles.split}>
        <div className={styles.splitTable}>
          {error ? (
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              <ErrorMessage message={error.message} />
              <button className="btn-primary" style={{ alignSelf: "flex-start" }} onClick={() => refetch()}>
                <RefreshCw size={13} /> {t("common.retry")}
              </button>
            </div>
          ) : (
          <DataTable
            columns={columns}
            data={courses}
            loading={isLoading}
            emptyIcon={BookOpen}
            emptyTitle={t("courses.noCourses")}
            emptyMessage={t("courses.noCoursesHint")}
            emptyActionLabel={t("courses.createCourse")}
            emptyAction={() => { setDrawerMode("create"); setEditTarget(null); }}
            pagination={{ pageNumber: page, totalPages }}
            onPageChange={setPage}
            onRowClick={(row) => setActiveCourse(row)}
            getRowClass={(row) => (panelCourse?.id === row.id ? "warn" : undefined)}
            selectedIds={selectedIds}
            onSelectAll={() => {
              if (courses.every((c) => selectedIds.has(c.id))) setSelectedIds(new Set());
              else setSelectedIds(new Set(courses.map((c) => c.id)));
            }}
            onSelectOne={(id) => {
              setSelectedIds((prev) => {
                const n = new Set(prev);
                if (n.has(id)) n.delete(id); else n.add(id);
                return n;
              });
            }}
            compact
            tableLabel={t("courses.title")}
          />
          )}

          {selectedIds.size > 0 && (
            <div className={shared.bulkBar}>
              <span>{t("common.selected", { count: selectedIds.size })}</span>
              <button onClick={() => setSelectedIds(new Set())}>{t("common.clear")}</button>
              <PermissionGate resource="courses.courses" minLevel={5}>
                <button className={shared.bulkDanger} onClick={handleBulkDelete} disabled={bulkDelete.isPending}>
                  <Trash2 size={12} /> {t("common.deleteSelected")}
                </button>
              </PermissionGate>
            </div>
          )}
        </div>

        <aside className={styles.splitPanel}>
          <CourseDetailPanel
            course={panelCourse}
            semester={selectedSemesterObj}
            categoryLabel={panelCourse ? t(`categories.${panelCourse.category}`) : ""}
            canEditPrereqs={canEdit}
          />
        </aside>
      </div>

      <CourseFormDrawer
        open={!!drawerMode}
        mode={drawerMode}
        course={editTarget}
        onClose={() => { setDrawerMode(null); setEditTarget(null); setServerError(""); }}
        onSubmit={handleSubmit}
        saving={createCourse.isPending || updateCourse.isPending}
        serverError={serverError}
      />

      <ConfirmDialog
        open={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleDelete}
        title={t("courses.deleteCourse")}
        message={t("courses.deleteCourseMsg", { code: deleteTarget?.code })}
        detail={t("courses.deleteCourseDetail")}
        confirmLabel={t("common.delete")}
        variant="danger"
        loading={deleteCourse.isPending}
      />

      <ConfirmDialog
        open={!!confirmAction}
        onClose={() => setConfirmAction(null)}
        onConfirm={handleLifecycle}
        title={confirmAction?.type === "close" ? t("courses.closeRecord") : t("courses.reopenRecord")}
        message={confirmAction?.type === "close"
          ? t("courses.closeRecordMsg", { code: confirmAction?.course?.code })
          : t("courses.reopenRecordMsg", { code: confirmAction?.course?.code })}
        detail={confirmAction?.type === "close" ? t("courses.closeRecordDetail") : t("courses.reopenRecordDetail")}
        confirmLabel={confirmAction?.type === "close" ? t("common.close") : t("common.reopen")}
        variant={confirmAction?.type === "close" ? "warning" : "default"}
        loading={closeCourse.isPending || openCourse.isPending}
      />
    </>
  );
}
