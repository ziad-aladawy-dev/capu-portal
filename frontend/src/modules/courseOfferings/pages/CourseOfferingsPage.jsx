import { useState, useCallback, useEffect, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  CalendarCheck, Plus, Edit2, AlertTriangle, Search, X, Lock, Unlock,
  Send, Ban, ChevronLeft, ChevronRight, RefreshCw, Users, UserCheck,
  UserX, ArrowUp, ArrowDown,
} from "lucide-react";
import PermissionGate from "../../../core/auth/PermissionGate";
import { useDomain } from "../../../core/contexts/DomainContext";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import { OFFERING_STATUS_LABELS, REGISTRATION_STATE_LABELS } from "../../../core/services/courseOfferingService";
import * as courseOfferingService from "../../../core/services/courseOfferingService";
import { useToast } from "../../../core/components/Toast";
import { SkeletonTable } from "../../../core/components/Skeleton";
import EmptyState from "../../../core/components/EmptyState";
import BulkActionBar from "../../../core/components/BulkActionBar";
import OfferingForm from "../components/OfferingForm";
import "../styles/courseOfferings.css";

const PAGE_SIZE = 20;

function CourseOfferingsPage() {
  const { t } = useTranslation();
  const { addToast } = useToast();
  const { scopeNode } = useDomain();
  const { selectedSemesterObj, selectedSemester } = useAcademic();

  const [offerings, setOfferings] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [courses, setCourses] = useState([]);
  const [faculties, setFaculties] = useState([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [refreshKey, setRefreshKey] = useState(0);
  const [selectedIds, setSelectedIds] = useState(new Set());

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");

  const [showForm, setShowForm] = useState(false);
  const [editOffering, setEditOffering] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

  const [lifecycleLoading, setLifecycleLoading] = useState(null);
  const [cancelReason, setCancelReason] = useState("");
  const [showCancelInput, setShowCancelInput] = useState(false);
  const [capacityAdjusting, setCapacityAdjusting] = useState(null);

  useEffect(() => {
    courseServiceInit();
  }, []);

  async function courseServiceInit() {
    const mod = await import("../../../core/services/courseService");
    const struct = await import("../../../core/services/structureService");
    try {
      const [courseData, facultyData] = await Promise.all([
        mod.fetchActiveCourses(),
        struct.fetchFaculties(),
      ]);
      setCourses(Array.isArray(courseData) ? courseData : []);
      setFaculties(Array.isArray(facultyData) ? facultyData : []);
    } catch {}
  }

  const loadOfferings = useCallback(async (p = 1) => {
    if (!scopeNode?.id || !selectedSemesterObj?.id) return;
    setLoading(true);
    setError(null);
    try {
      const params = {
        Page: p,
        PageSize: PAGE_SIZE,
        StructureNodeId: scopeNode.id,
        SemesterId: selectedSemesterObj.id,
        Search: search.trim() || undefined,
        Status: statusFilter !== "" ? Number(statusFilter) : undefined,
      };
      const result = await courseOfferingService.searchCourseOfferings(params);
      setOfferings(Array.isArray(result?.items) ? result.items : []);
      setTotalCount(result?.totalCount || 0);
      setPage(p);
    } catch (err) {
      setError(err.message || "Failed to load offerings");
      setOfferings([]);
    } finally {
      setLoading(false);
    }
  }, [scopeNode?.id, selectedSemesterObj?.id, search, statusFilter]);

  useEffect(() => { loadOfferings(1); }, [refreshKey, scopeNode?.id, selectedSemesterObj?.id]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  const handleSearch = () => { setPage(1); setSelectedIds(new Set()); loadOfferings(1); };

  const getCourseInfo = (courseId) => courses.find((c) => c.id === courseId);

  const stats = useMemo(() => {
    const total = offerings.length;
    const atCapacity = offerings.filter((o) => o.capacity > 0 && o.registeredCount >= o.capacity).length;
    const nearCapacity = offerings.filter((o) => o.capacity > 0 && o.registeredCount / o.capacity >= 0.8 && o.registeredCount < o.capacity).length;
    const regOpen = offerings.filter((o) => o.registrationState === 1).length;
    const draft = offerings.filter((o) => o.status === 0).length;
    return { total, atCapacity, nearCapacity, regOpen, draft };
  }, [offerings]);

  const handleCapacityAdjust = async (offering, delta) => {
    setCapacityAdjusting(offering.id);
    try {
      const newCapacity = Math.max(0, offering.capacity + delta);
      await courseOfferingService.updateCourseOffering(offering.id, { capacity: newCapacity });
      addToast(`Capacity ${delta > 0 ? "increased" : "decreased"} to ${newCapacity}`, "success");
      setRefreshKey(k => k + 1);
    } catch (err) {
      addToast(err.message || "Failed to adjust capacity", "error");
    } finally {
      setCapacityAdjusting(null);
    }
  };

  const openCreate = () => {
    setEditOffering(null);
    setFormError("");
    setShowForm(true);
  };

  const openEdit = (offering) => {
    setEditOffering(offering);
    setFormError("");
    setShowForm(true);
  };

  const closeForm = () => {
    setShowForm(false);
    setEditOffering(null);
    setFormError("");
  };

  const handleSave = async (formData) => {
    setSaving(true);
    setFormError("");
    try {
      if (editOffering) {
        const body = {};
        if (formData.sectionCode !== editOffering.sectionCode) body.sectionCode = formData.sectionCode;
        if (formData.capacity !== editOffering.capacity) body.capacity = formData.capacity;
        if (formData.status !== editOffering.status) body.status = formData.status;
        if (formData.registrationState !== editOffering.registrationState) body.registrationState = formData.registrationState;
        await courseOfferingService.updateCourseOffering(editOffering.id, body);
        addToast("Offering updated", "success");
      } else {
        await courseOfferingService.createCourseOffering(formData);
        addToast("Offering created", "success");
      }
      closeForm();
      setRefreshKey(k => k + 1);
    } catch (err) {
      setFormError(err.message || "Failed to save offering");
    } finally {
      setSaving(false);
    }
  };

  const handleClose = async (id) => {
    setLifecycleLoading(id);
    try {
      await courseOfferingService.closeCourseOffering(id);
      addToast("Offering closed", "success");
      setRefreshKey(k => k + 1);
    } catch (err) {
      addToast(err.message || "Failed to close", "error");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const handleOpen = async (id) => {
    setLifecycleLoading(id);
    try {
      await courseOfferingService.openCourseOffering(id);
      addToast("Offering reopened", "success");
      setRefreshKey(k => k + 1);
    } catch (err) {
      addToast(err.message || "Failed to reopen", "error");
    } finally {
      setLifecycleLoading(null);
    }
  };

  const allSelected = offerings.length > 0 && offerings.every(o => selectedIds.has(o.id));
  const someSelected = offerings.some(o => selectedIds.has(o.id));

  const handleSelectAll = () => {
    if (allSelected) setSelectedIds(new Set());
    else setSelectedIds(new Set(offerings.map(o => o.id)));
  };

  const handleSelectOne = (id) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const handleBulkPublish = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length) return;
    try {
      await courseOfferingService.bulkPublishOfferings(ids);
      addToast(`${ids.length} offering(s) published`, "success");
      setSelectedIds(new Set());
      setRefreshKey(k => k + 1);
    } catch (err) {
      addToast(`Publish failed: ${err.message}`, "error");
    }
  };

  const handleBulkCancel = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length || !cancelReason.trim()) return;
    try {
      await courseOfferingService.bulkCancelOfferings(ids, cancelReason);
      addToast(`${ids.length} offering(s) cancelled`, "success");
      setSelectedIds(new Set());
      setCancelReason("");
      setShowCancelInput(false);
      setRefreshKey(k => k + 1);
    } catch (err) {
      addToast(`Cancel failed: ${err.message}`, "error");
    }
  };

  return (
    <div className="co-page">
      <div className="co-header">
        <div className="co-header-left">
          <CalendarCheck size={20} />
          <div>
            <h1>{t("course_offerings")}</h1>
            <p>{t("manage_course_offerings")} — {selectedSemester}</p>
          </div>
        </div>
        <PermissionGate resource="course-offerings.course-offerings" minLevel={2}>
          <button className="co-btn co-btn-primary" onClick={openCreate}>
            <Plus size={16} /> {t("new_offering")}
          </button>
        </PermissionGate>
      </div>

      {error && (
        <div className="co-alert co-alert-error" style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <AlertTriangle size={16} /> {error}
          <button className="co-btn co-btn-outline" style={{ marginLeft: "auto", padding: "4px 10px", fontSize: 12 }}
            onClick={() => setRefreshKey(k => k + 1)}>
            <RefreshCw size={11} /> Retry
          </button>
        </div>
      )}

      <div className="co-toolbar">
        <div className="co-search">
          <Search size={14} />
          <input type="text" placeholder={t("search_course_offerings")} value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && handleSearch()} />
          {search && (
            <button className="co-search-clear" onClick={() => { setSearch(""); setRefreshKey(k => k + 1); }}>
              <X size={14} />
            </button>
          )}
        </div>
        <select className="co-select" value={statusFilter}
          onChange={(e) => { setStatusFilter(e.target.value); setRefreshKey(k => k + 1); }}>
          <option value="">{t("all_statuses")}</option>
          {Object.entries(OFFERING_STATUS_LABELS).map(([val, label]) => (
            <option key={val} value={val}>{label}</option>
          ))}
        </select>
        <button className="co-btn co-btn-outline" onClick={handleSearch}>
          <Search size={13} /> {t("search")}
        </button>
      </div>

      {offerings.length > 0 && (
        <div className="co-stats-bar">
          <div className="co-stat-item">
            <span className="co-stat-value">{stats.total}</span>
            <span className="co-stat-label">Total</span>
          </div>
          <div className="co-stat-divider" />
          <div className="co-stat-item warn">
            <span className="co-stat-value">{stats.nearCapacity}</span>
            <span className="co-stat-label">Near Cap</span>
          </div>
          <div className="co-stat-item danger">
            <span className="co-stat-value">{stats.atCapacity}</span>
            <span className="co-stat-label">Full</span>
          </div>
          <div className="co-stat-divider" />
          <div className="co-stat-item success">
            <span className="co-stat-value">{stats.regOpen}</span>
            <span className="co-stat-label">Reg Open</span>
          </div>
          <div className="co-stat-item">
            <span className="co-stat-value">{stats.draft}</span>
            <span className="co-stat-label">Draft</span>
          </div>
        </div>
      )}

      {stats.atCapacity > 0 && (
        <div className="co-alert co-alert-warning">
          <Users size={14} />
          <strong>{stats.atCapacity} offering(s)</strong> at full capacity
          {stats.nearCapacity > 0 && (
            <span> &middot; <strong>{stats.nearCapacity} more</strong> near capacity</span>
          )}
        </div>
      )}

      {loading ? (
        <SkeletonTable rows={8} cols={7} />
      ) : offerings.length === 0 ? (
        <EmptyState icon={CalendarCheck} title={t("no_course_offerings")}
          message={t("create_offering_to_start")} actionLabel={t("new_offering")} onAction={openCreate} />
      ) : (
        <div className="co-table-wrap">
          <table className="co-table">
            <thead>
              <tr>
                <th style={{ width: 36 }}>
                  <input type="checkbox" checked={allSelected} ref={el => { if (el) el.indeterminate = someSelected && !allSelected; }}
                    onChange={handleSelectAll} />
                </th>
                <th>#</th>
                <th>{t("course")}</th>
                <th>{t("section")}</th>
                <th>{t("capacity")}</th>
                <th>{t("enrolled")}</th>
                <th>{t("status")}</th>
                <th>{t("registration")}</th>
                <th>Record</th>
                <th>{t("actions")}</th>
              </tr>
            </thead>
            <tbody>
              {offerings.map((offering, idx) => {
                const course = getCourseInfo(offering.courseId);
                const statusLabel = OFFERING_STATUS_LABELS[offering.status] || t("unknown");
                const regLabel = REGISTRATION_STATE_LABELS[offering.registrationState] || t("unknown");
                const filled = offering.capacity > 0 ? Math.round((offering.registeredCount / offering.capacity) * 100) : 0;
                return (
                  <tr key={offering.id} className={`${selectedIds.has(offering.id) ? "selected-row" : ""} ${filled >= 100 ? "co-row-full" : filled >= 80 ? "co-row-warn" : ""}`}>
                    <td onClick={e => e.stopPropagation()}>
                      <input type="checkbox" checked={selectedIds.has(offering.id)} onChange={() => handleSelectOne(offering.id)} />
                    </td>
                    <td style={{ color: "#6b7280" }}>{(page - 1) * PAGE_SIZE + idx + 1}</td>
                    <td>
                      <div className="co-course-info">
                        <span className="co-course-code">{course?.code || "—"}</span>
                        <span className="co-course-title">{course?.title || t("unknown_course")}</span>
                      </div>
                    </td>
                    <td><span className="co-section-badge">{offering.sectionCode}</span></td>
                    <td>
                      <div className="co-capacity-control">
                        <span className="co-capacity-num">{offering.capacity}</span>
                        <PermissionGate resource="course-offerings.course-offerings" minLevel={3}>
                          <div className="co-capacity-adjust">
                            <button
                              className="co-capacity-btn"
                              onClick={() => handleCapacityAdjust(offering, 1)}
                              disabled={capacityAdjusting === offering.id}
                              title="Increase capacity by 1"
                            >
                              <ArrowUp size={10} />
                            </button>
                            <button
                              className="co-capacity-btn"
                              onClick={() => handleCapacityAdjust(offering, -1)}
                              disabled={capacityAdjusting === offering.id || offering.capacity <= 0}
                              title="Decrease capacity by 1"
                            >
                              <ArrowDown size={10} />
                            </button>
                          </div>
                        </PermissionGate>
                      </div>
                    </td>
                    <td>
                      <div className="co-enrolled-cell">
                        <span className={`co-enrolled-num ${filled >= 100 ? "danger" : filled >= 80 ? "warn" : ""}`}>
                          {offering.registeredCount}
                        </span>
                        <div className="co-capacity-bar">
                          <div className={`co-capacity-fill ${filled >= 100 ? "full" : filled >= 80 ? "warn" : "ok"}`}
                            style={{ width: `${Math.min(filled, 100)}%` }} />
                        </div>
                      </div>
                    </td>
                    <td><span className={`co-status-badge status-${statusLabel.toLowerCase()}`}>{statusLabel}</span></td>
                    <td><span className={`co-reg-badge reg-${regLabel.toLowerCase()}`}>{regLabel}</span></td>
                    <td>
                      <span className={`co-status-badge ${offering.isClosed ? "status-closed" : "status-open"}`} style={{ fontSize: 11 }}>
                        {offering.isClosed ? "Closed" : "Open"}
                      </span>
                    </td>
                    <td>
                      <div style={{ display: "flex", gap: 4 }}>
                        <PermissionGate resource="course-offerings.course-offerings" minLevel={3}>
                          <button className="co-action-btn" onClick={() => openEdit(offering)} title={t("edit")}>
                            <Edit2 size={14} />
                          </button>
                        </PermissionGate>
                        {offering.isClosed ? (
                          <PermissionGate resource="course-offerings.course-offerings" minLevel={4}>
                            <button className="co-action-btn" onClick={() => handleOpen(offering.id)}
                              disabled={lifecycleLoading === offering.id} title="Reopen">
                              <Unlock size={14} />
                            </button>
                          </PermissionGate>
                        ) : (
                          <PermissionGate resource="course-offerings.course-offerings" minLevel={3}>
                            <button className="co-action-btn" onClick={() => handleClose(offering.id)}
                              disabled={lifecycleLoading === offering.id} title="Close">
                              <Lock size={14} />
                            </button>
                          </PermissionGate>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {totalPages > 1 && (
        <div style={{ display: "flex", justifyContent: "center", alignItems: "center", gap: 8, marginTop: 20 }}>
          <button className="co-btn co-btn-outline" disabled={page <= 1} onClick={() => loadOfferings(page - 1)}>
            <ChevronLeft size={14} /> {t("previous")}
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
              <button key={pn} className={`co-btn ${pn === page ? "co-btn-primary" : "co-btn-outline"}`}
                style={{ minWidth: 36, justifyContent: "center" }} onClick={() => loadOfferings(pn)}>
                {pn}
              </button>
            );
          })}
          <button className="co-btn co-btn-outline" disabled={page >= totalPages} onClick={() => loadOfferings(page + 1)}>
            {t("next")} <ChevronRight size={14} />
          </button>
        </div>
      )}

      {showCancelInput && (
        <div className="co-alert" style={{ marginTop: 12, padding: 12, background: "#fffbeb", border: "1px solid #fcd34d", borderRadius: 8 }}>
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            <input type="text" placeholder="Enter cancellation reason…" value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              style={{ flex: 1, padding: "8px 12px", border: "1px solid #d1d5db", borderRadius: 8, fontSize: 13 }} />
            <button className="co-btn co-btn-outline" onClick={() => setShowCancelInput(false)}>Cancel</button>
            <button className="co-btn co-btn-primary" onClick={handleBulkCancel} disabled={!cancelReason.trim()}>
              <Ban size={13} /> Confirm Cancel
            </button>
          </div>
        </div>
      )}

      <BulkActionBar
        selectedCount={selectedIds.size}
        onClear={() => setSelectedIds(new Set())}
        actions={[
          {
            label: "Publish", icon: <Send size={13} />, variant: "success",
            onClick: handleBulkPublish, requiresPermission: true,
            permissionResource: "course-offerings.course-offerings", permissionLevel: 3,
          },
          {
            label: "Cancel", icon: <Ban size={13} />, variant: "warning",
            onClick: () => setShowCancelInput(true), requiresPermission: true,
            permissionResource: "course-offerings.course-offerings", permissionLevel: 3,
          },
        ]}
      />

      {showForm && (
        <OfferingForm
          editOffering={editOffering}
          courses={courses}
          faculties={faculties}
          semesterId={selectedSemesterObj?.id}
          structureNodeId={scopeNode?.id}
          saving={saving}
          formError={formError}
          onSave={handleSave}
          onClose={closeForm}
        />
      )}
    </div>
  );
}

export default CourseOfferingsPage;
