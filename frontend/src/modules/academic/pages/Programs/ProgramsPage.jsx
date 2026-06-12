import { useState, useEffect, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  GraduationCap, Plus, Pencil, Trash2, Search, X, AlertCircle,
  Building2, Layers, Library, ClipboardList,
} from "lucide-react";
import * as structureService from "../../../../core/services/structureService";
import { getNodeTypeValue } from "../../../../core/constants/nodeTypeRegistry";
import PermissionGate from "../../../../core/auth/PermissionGate";
import { useToast } from "../../../../core/components/Toast";
import { getLocalized } from "../../../../core/utils/getLocalized";
import DataTable from "../../../../core/components/DataTable";
import Drawer from "../../../../core/components/Drawer";
import ConfirmDialog from "../../../../core/components/ConfirmDialog";
import { useAcademicPlans } from "../../../../core/query/useAcademicPlans";
import shared from "../../styles/academic.module.css";
import "./programs.css";

// Lookup endpoints return name as a bilingual JSON string — getLocalized parses both shapes.
const nodeName = (n) => getLocalized(n?.name, "en", "—") || "—";
const nodeNameAr = (n) => getLocalized(n?.name, "ar", "");

// Strict split for form fields — no cross-language fallback.
const parseBilingual = (name) => {
  let o = name;
  if (typeof name === "string") {
    try { o = JSON.parse(name); } catch { o = { en: name }; }
  }
  return { en: o?.en || "", ar: o?.ar || "" };
};

function ProgramsPage() {
  const { t } = useTranslation("academic");
  const { addToast } = useToast();
  const navigate = useNavigate();

  const [programs, setPrograms] = useState([]);
  const [faculties, setFaculties] = useState([]);
  const [parentNodes, setParentNodes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [search, setSearch] = useState("");
  const [facultyFilter, setFacultyFilter] = useState("");
  const [selectedIds, setSelectedIds] = useState(new Set());

  const [drawer, setDrawer] = useState(null); // "create" | "edit" | null
  const [editing, setEditing] = useState(null);
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState(null);
  const [bulkDeleting, setBulkDeleting] = useState(false);

  // Plan counts per program — one wide page covers a university-scale plan set.
  const { data: planData } = useAcademicPlans({ page: 1, pageSize: 500 });
  const planCountByProgram = useMemo(() => {
    const m = new Map();
    for (const p of planData?.items || []) {
      m.set(p.structureNodeId, (m.get(p.structureNodeId) || 0) + 1);
    }
    return m;
  }, [planData]);

  const schema = z.object({
    nameEn: z.string().trim().min(1, t("programs.validation.nameRequired")),
    nameAr: z.string().trim().optional().or(z.literal("")),
    facultyId: z.string().optional().or(z.literal("")),
  });

  const { register, handleSubmit, formState: { errors }, reset } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { nameEn: "", nameAr: "", facultyId: "" },
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [progs, facs] = await Promise.all([
        structureService.fetchPrograms(),
        structureService.fetchFaculties(),
      ]);
      const programList = Array.isArray(progs) ? progs : [];
      setPrograms(programList);
      setFaculties(Array.isArray(facs) ? facs : []);

      // Programs may hang off intermediate nodes (e.g. education systems) that no
      // flat lookup covers — resolve the distinct parents directly.
      const parentIds = [...new Set(programList.map((p) => p.parentId).filter(Boolean))];
      const parents = await Promise.all(
        parentIds.map((id) => structureService.fetchStructureNode(id).catch(() => null))
      );
      setParentNodes(parents.filter(Boolean));
    } catch (err) {
      setError(err.message || "Failed to load programs");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  // Programs hang off either a faculty or an intermediate structure node.
  const parentById = useMemo(() => {
    const m = {};
    faculties.forEach((f) => (m[f.id] = f));
    parentNodes.forEach((d) => (m[d.id] = d));
    return m;
  }, [faculties, parentNodes]);

  // Filter options = only nodes that are actually some program's parent.
  // Same-named system nodes exist under different faculties — suffix the faculty to disambiguate.
  const parentOptions = useMemo(() => {
    const facById = {};
    faculties.forEach((f) => (facById[f.id] = f));
    const seen = new Map();
    programs.forEach((p) => {
      const parent = p.parentId ? parentById[p.parentId] : null;
      if (parent && !seen.has(parent.id)) {
        const fac = parent.parentId ? facById[parent.parentId] : null;
        seen.set(parent.id, fac ? `${nodeName(parent)} — ${nodeName(fac)}` : nodeName(parent));
      }
    });
    return [...seen.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  }, [programs, parentById, faculties]);

  const getParentName = (parentId) => (parentId && parentById[parentId] ? nodeName(parentById[parentId]) : null);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return programs.filter((p) => {
      if (facultyFilter && p.parentId !== facultyFilter) return false;
      if (!q) return true;
      const parent = p.parentId ? parentById[p.parentId] : null;
      const hay = `${nodeName(p)} ${nodeNameAr(p)} ${parent ? nodeName(parent) : ""}`.toLowerCase();
      return hay.includes(q);
    });
  }, [programs, search, facultyFilter, parentById]);

  const stats = useMemo(() => {
    const orphaned = programs.filter((p) => !p.parentId || !parentById[p.parentId]).length;
    return { total: programs.length, faculties: faculties.length, shown: filtered.length, orphaned };
  }, [programs, faculties, filtered, parentById]);

  const openCreate = () => {
    setEditing(null);
    reset({ nameEn: "", nameAr: "", facultyId: "" });
    setFormError("");
    setDrawer("create");
  };

  const openEdit = (program) => {
    setEditing(program);
    const names = parseBilingual(program.name);
    reset({ nameEn: names.en, nameAr: names.ar, facultyId: program.parentId || "" });
    setFormError("");
    setDrawer("edit");
  };

  const closeDrawer = () => {
    setDrawer(null);
    setEditing(null);
    setFormError("");
  };

  const onSubmit = handleSubmit(async (form) => {
    setSaving(true);
    setFormError("");
    try {
      // The structure endpoints take flat name (ar) + nameEn strings and compile
      // them into the stored {"ar","en"} JSON server-side.
      const names = {
        name: form.nameAr.trim() || form.nameEn.trim(),
        nameEn: form.nameEn.trim() || form.nameAr.trim(),
      };
      if (editing) {
        // PUT overwrites type/order/isActive unconditionally — preserve them.
        const node = await structureService.fetchStructureNode(editing.id);
        await structureService.updateStructureNode(editing.id, {
          ...names,
          type: node?.type ?? getNodeTypeValue("Program"),
          order: node?.order ?? 0,
          isActive: node?.isActive ?? true,
        });
        addToast(t("programs.programUpdated"), "success");
      } else {
        await structureService.createStructureNode({
          ...names,
          type: getNodeTypeValue("Program"),
          parentId: form.facultyId || null,
          order: 0,
        });
        addToast(t("programs.programCreated"), "success");
      }
      closeDrawer();
      await load();
    } catch (err) {
      setFormError(err.message || t("courses.saveFailed"));
    } finally {
      setSaving(false);
    }
  });

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await structureService.deleteStructureNode(deleteTarget.id);
      addToast(t("programs.programDeleted"), "success");
      setSelectedIds((prev) => { const n = new Set(prev); n.delete(deleteTarget.id); return n; });
      setDeleteTarget(null);
      await load();
    } catch (err) {
      addToast(err.message || t("courses.saveFailed"), "error");
      setDeleteTarget(null);
    }
  };

  const handleBulkDelete = async () => {
    const ids = Array.from(selectedIds);
    if (!ids.length) return;
    setBulkDeleting(true);
    try {
      const results = await Promise.allSettled(ids.map((id) => structureService.deleteStructureNode(id)));
      const ok = results.filter((r) => r.status === "fulfilled").length;
      addToast(`${ok} / ${ids.length}`, ok === ids.length ? "success" : "warning");
      setSelectedIds(new Set());
      await load();
    } catch (err) {
      addToast(err.message || t("courses.saveFailed"), "error");
    } finally {
      setBulkDeleting(false);
    }
  };

  const toggleSelectAll = () => {
    if (filtered.every((p) => selectedIds.has(p.id))) setSelectedIds(new Set());
    else setSelectedIds(new Set(filtered.map((p) => p.id)));
  };
  const toggleSelectOne = (id) =>
    setSelectedIds((prev) => { const n = new Set(prev); if (n.has(id)) n.delete(id); else n.add(id); return n; });

  const columns = [
    {
      key: "name", label: t("programs.title"), nowrap: false,
      render: (_, row) => <strong style={{ color: "#1a1f5e" }}>{nodeName(row)}</strong>,
    },
    {
      key: "nameAr", label: t("programs.nameAr"), nowrap: false,
      render: (_, row) => <span style={{ direction: "rtl", display: "inline-block" }}>{parseBilingual(row.name).ar || "—"}</span>,
    },
    {
      key: "parentId", label: t("programs.parent"),
      render: (v) => {
        const parentName = getParentName(v);
        return parentName
          ? <span className="prog-badge prog-badge-primary">{parentName}</span>
          : <span className="prog-badge prog-badge-outline">{t("offerings.unassigned")}</span>;
      },
    },
    {
      key: "_plans", label: t("programs.plansCount"), align: "center", width: 80,
      render: (_, row) => {
        const n = planCountByProgram.get(row.id) || 0;
        return n ? (
          <button
            type="button"
            className={shared.metaChip}
            style={{ cursor: "pointer", border: "1px solid #c7d2fe", background: "#eef2ff", color: "#1a1f5e" }}
            onClick={(e) => { e.stopPropagation(); navigate("/admin/academic/plans"); }}
            title={t("nav.plans")}
          >
            <ClipboardList size={11} /> {n}
          </button>
        ) : <span style={{ color: "#d1d5db" }}>—</span>;
      },
    },
    {
      key: "actions", label: t("common.actions"), align: "right", nowrap: true,
      render: (_, row) => (
        <div className="prog-action-buttons" onClick={(e) => e.stopPropagation()}>
          <PermissionGate resource="programs.programs" minLevel={3}>
            <button className="btn-icon" title={t("common.edit")} onClick={() => openEdit(row)}><Pencil size={14} /></button>
          </PermissionGate>
          <PermissionGate resource="programs.programs" minLevel={5}>
            <button className="btn-icon btn-icon-danger" title={t("common.delete")} onClick={() => setDeleteTarget(row)}><Trash2 size={14} /></button>
          </PermissionGate>
        </div>
      ),
    },
  ];

  return (
    <div className="prog-page" style={{ padding: 0 }}>
      <div className="prog-header">
        <div className="prog-header-left">
          <GraduationCap size={24} />
          <div>
            <h1>{t("programs.title")}</h1>
            <p>{t("programs.subtitle")}</p>
          </div>
        </div>
        <PermissionGate resource="programs.programs" minLevel={2}>
          <button className="btn btn-primary" onClick={openCreate}>
            <Plus size={16} /> {t("programs.createProgram")}
          </button>
        </PermissionGate>
      </div>

      {error && (
        <div className="prog-alert prog-alert-error" role="alert">
          <AlertCircle size={16} /> {error}
          <button onClick={() => setError(null)} aria-label={t("common.close")}>&times;</button>
        </div>
      )}

      {/* Stats */}
      <div className="prog-stats-grid">
        <div className="prog-stat-card">
          <div className="prog-stat-icon" style={{ background: "#eef2ff", color: "#4f46e5" }}><Library size={22} /></div>
          <div><div className="prog-stat-value">{stats.total}</div><div className="prog-stat-label">{t("programs.title")}</div></div>
        </div>
        <div className="prog-stat-card">
          <div className="prog-stat-icon" style={{ background: "#f0fdf4", color: "#16a34a" }}><Building2 size={22} /></div>
          <div><div className="prog-stat-value">{stats.faculties}</div><div className="prog-stat-label">{t("programs.faculty")}</div></div>
        </div>
        <div className="prog-stat-card">
          <div className="prog-stat-icon" style={{ background: "#f0f9ff", color: "#0284c7" }}><Search size={22} /></div>
          <div><div className="prog-stat-value">{stats.shown}</div><div className="prog-stat-label">{t("common.search")}</div></div>
        </div>
        <div className="prog-stat-card">
          <div className="prog-stat-icon" style={{ background: "#fffbeb", color: "#d97706" }}><Layers size={22} /></div>
          <div><div className="prog-stat-value">{stats.orphaned}</div><div className="prog-stat-label">{t("offerings.unassigned")}</div></div>
        </div>
      </div>

      {/* Toolbar */}
      <div className="prog-toolbar">
        <div className="prog-search-bar" style={{ margin: 0, flex: 1 }}>
          <Search size={16} />
          <input
            type="text"
            placeholder={t("programs.searchPlaceholder")}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          {search && <button style={{ background: "none", border: "none", cursor: "pointer", color: "#9ca3af", display: "flex" }} onClick={() => setSearch("")}><X size={14} /></button>}
        </div>
        <div className="prog-filter-group">
          <Building2 size={15} />
          <select className="form-select" style={{ width: "auto", minWidth: 180 }} value={facultyFilter} onChange={(e) => setFacultyFilter(e.target.value)} aria-label={t("programs.faculty")}>
            <option value="">{t("programs.allFaculties")}</option>
            {parentOptions.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
          </select>
        </div>
        {(search || facultyFilter) && (
          <button className="btn btn-ghost" onClick={() => { setSearch(""); setFacultyFilter(""); }}>
            <X size={14} /> {t("common.clear")}
          </button>
        )}
      </div>

      <DataTable
        columns={columns}
        data={filtered}
        loading={loading}
        emptyIcon={GraduationCap}
        emptyTitle={t("programs.noPrograms")}
        emptyMessage={t("programs.noProgramsHint")}
        emptyActionLabel={t("programs.createProgram")}
        emptyAction={openCreate}
        rowKey="id"
        selectedIds={selectedIds}
        onSelectAll={toggleSelectAll}
        onSelectOne={toggleSelectOne}
        compact
        tableLabel={t("programs.title")}
      />

      {/* Bulk bar */}
      {selectedIds.size > 0 && (
        <div className={shared.bulkBar}>
          <span>{t("common.selected", { count: selectedIds.size })}</span>
          <button onClick={() => setSelectedIds(new Set())}>{t("common.clear")}</button>
          <PermissionGate resource="programs.programs" minLevel={5}>
            <button className={shared.bulkDanger} onClick={handleBulkDelete} disabled={bulkDeleting}>
              <Trash2 size={13} /> {t("common.deleteSelected")}
            </button>
          </PermissionGate>
        </div>
      )}

      {/* Create/Edit Drawer */}
      <Drawer
        open={!!drawer}
        onClose={closeDrawer}
        title={drawer === "create" ? t("programs.createProgram") : t("programs.editProgram")}
        width={440}
        loading={saving}
        footer={
          <>
            <button className="btn btn-ghost" onClick={closeDrawer}>{t("common.cancel")}</button>
            <button className="btn btn-primary" onClick={onSubmit} disabled={saving}>
              {saving ? t("common.saving") : drawer === "create" ? t("common.create") : t("common.save")}
            </button>
          </>
        }
      >
        {formError && (
          <div role="alert" className={shared.errorBanner}>{formError}</div>
        )}
        <form onSubmit={onSubmit}>
          <div className={shared.formGroup}>
            <label htmlFor="prog-name-en">{t("programs.nameEn")} *</label>
            <input id="prog-name-en" className={shared.formInput} type="text" autoFocus
              placeholder="e.g. Computer Science" {...register("nameEn")} />
            {errors.nameEn && <span className={shared.formError}>{errors.nameEn.message}</span>}
          </div>
          <div className={shared.formGroup}>
            <label htmlFor="prog-name-ar">{t("programs.nameAr")}</label>
            <input id="prog-name-ar" className={shared.formInput} type="text" dir="rtl"
              placeholder="مثال: علوم الحاسب" {...register("nameAr")} />
          </div>
          <div className={shared.formGroup}>
            <label htmlFor="prog-faculty">{t("programs.parent")}</label>
            <select id="prog-faculty" className={shared.formInput} {...register("facultyId")}>
              <option value="">{t("offerings.unassigned")}</option>
              <optgroup label={t("programs.faculty")}>
                {faculties.map((f) => <option key={f.id} value={f.id}>{nodeName(f)}</option>)}
              </optgroup>
              {parentNodes.length > 0 && (
                <optgroup label={t("programs.parent")}>
                  {parentNodes.filter((d) => !faculties.some((f) => f.id === d.id)).map((d) => (
                    <option key={d.id} value={d.id}>{nodeName(d)}</option>
                  ))}
                </optgroup>
              )}
            </select>
          </div>
          <button type="submit" style={{ display: "none" }} aria-hidden="true" />
        </form>
      </Drawer>

      <ConfirmDialog
        open={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleDelete}
        title={t("programs.deleteProgram")}
        message={t("programs.deleteProgramMsg", { name: nodeName(deleteTarget) })}
        detail={t("programs.deleteProgramDetail")}
        confirmLabel={t("common.delete")}
        variant="danger"
      />
    </div>
  );
}

export default ProgramsPage;
