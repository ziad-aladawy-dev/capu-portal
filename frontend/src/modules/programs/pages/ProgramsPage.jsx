import { useState, useEffect, useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  GraduationCap, Plus, Pencil, Trash2, Search, X, AlertCircle,
  Building2, Layers, Library,
} from "lucide-react";
import * as structureService from "../../../core/services/structureService";
import PermissionGate from "../../../core/auth/PermissionGate";
import { useToast } from "../../../core/components/Toast";
import { getLocalized } from "../../../core/utils/getLocalized";
import DataTable from "../../../core/components/DataTable";
import Drawer from "../../../core/components/Drawer";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import "../styles/programs.css";

const EMPTY_FORM = { nameEn: "", nameAr: "", facultyId: "" };

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
  const { t } = useTranslation();
  const { addToast } = useToast();

  const [programs, setPrograms] = useState([]);
  const [faculties, setFaculties] = useState([]);
  const [parentNodes, setParentNodes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [search, setSearch] = useState("");
  const [facultyFilter, setFacultyFilter] = useState("");
  const [selectedIds, setSelectedIds] = useState(new Set());
  const [compact, setCompact] = useState(false);

  const [drawer, setDrawer] = useState(null); // "create" | "edit" | null
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(EMPTY_FORM);
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState(null);
  const [bulkDeleting, setBulkDeleting] = useState(false);

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
    setForm(EMPTY_FORM);
    setFormError("");
    setDrawer("create");
  };

  const openEdit = (program) => {
    setEditing(program);
    const names = parseBilingual(program.name);
    setForm({
      nameEn: names.en,
      nameAr: names.ar,
      facultyId: program.parentId || "",
    });
    setFormError("");
    setDrawer("edit");
  };

  const closeDrawer = () => {
    setDrawer(null);
    setEditing(null);
    setForm(EMPTY_FORM);
    setFormError("");
  };

  const handleSave = async (e) => {
    e?.preventDefault?.();
    if (!form.nameEn.trim()) { setFormError("English name is required"); return; }
    setSaving(true);
    setFormError("");
    try {
      const payload = {
        name: { en: form.nameEn.trim(), ar: form.nameAr.trim() },
        parentId: form.facultyId || null,
        type: "Program",
      };
      if (editing) {
        await structureService.updateStructureNode(editing.id, payload);
        addToast("Program updated", "success");
      } else {
        await structureService.createStructureNode(payload);
        addToast("Program created", "success");
      }
      closeDrawer();
      await load();
    } catch (err) {
      setFormError(err.message || "Failed to save program");
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await structureService.deleteStructureNode(deleteTarget.id);
      addToast("Program deleted", "success");
      setSelectedIds((prev) => { const n = new Set(prev); n.delete(deleteTarget.id); return n; });
      setDeleteTarget(null);
      await load();
    } catch (err) {
      addToast(err.message || "Failed to delete program", "error");
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
      addToast(`${ok} of ${ids.length} program(s) deleted`, ok === ids.length ? "success" : "warning");
      setSelectedIds(new Set());
      await load();
    } catch (err) {
      addToast(err.message || "Bulk delete failed", "error");
    } finally {
      setBulkDeleting(false);
    }
  };

  const toggleSelectAll = () => {
    if (filtered.every((p) => selectedIds.has(p.id))) setSelectedIds(new Set());
    else setSelectedIds(new Set(filtered.map((p) => p.id)));
  };
  const toggleSelectOne = (id) =>
    setSelectedIds((prev) => { const n = new Set(prev); n.has(id) ? n.delete(id) : n.add(id); return n; });

  const columns = [
    {
      key: "name", label: "Program", nowrap: false,
      render: (_, row) => <strong style={{ color: "#1a1f5e" }}>{nodeName(row)}</strong>,
    },
    {
      key: "nameAr", label: "Arabic Name", nowrap: false,
      render: (_, row) => <span style={{ direction: "rtl", display: "inline-block" }}>{parseBilingual(row.name).ar || "—"}</span>,
    },
    {
      key: "parentId", label: "Parent Node",
      render: (v) => {
        const parentName = getParentName(v);
        return parentName
          ? <span className="prog-badge prog-badge-primary">{parentName}</span>
          : <span className="prog-badge prog-badge-outline">Unassigned</span>;
      },
    },
    {
      key: "actions", label: "Actions", align: "right", nowrap: true,
      render: (_, row) => (
        <div className="prog-action-buttons" onClick={(e) => e.stopPropagation()}>
          <PermissionGate resource="programs.programs" minLevel={3}>
            <button className="btn-icon" title="Edit" onClick={() => openEdit(row)}><Pencil size={14} /></button>
          </PermissionGate>
          <PermissionGate resource="programs.programs" minLevel={5}>
            <button className="btn-icon btn-icon-danger" title="Delete" onClick={() => setDeleteTarget(row)}><Trash2 size={14} /></button>
          </PermissionGate>
        </div>
      ),
    },
  ];

  return (
    <div className="prog-page">
      <div className="prog-header">
        <div className="prog-header-left">
          <GraduationCap size={24} />
          <div>
            <h1>{t("academic_programs") || "Academic Programs"}</h1>
            <p>{t("manage_degree_programs") || "Create and organise degree programs across faculties."}</p>
          </div>
        </div>
        <PermissionGate resource="programs.programs" minLevel={2}>
          <button className="btn btn-primary" onClick={openCreate}>
            <Plus size={16} /> Add Program
          </button>
        </PermissionGate>
      </div>

      {error && (
        <div className="prog-alert prog-alert-error" role="alert">
          <AlertCircle size={16} /> {error}
          <button onClick={() => setError(null)} aria-label="Dismiss">&times;</button>
        </div>
      )}

      {/* Stats */}
      <div className="prog-stats-grid">
        <div className="prog-stat-card">
          <div className="prog-stat-icon" style={{ background: "#eef2ff", color: "#4f46e5" }}><Library size={22} /></div>
          <div><div className="prog-stat-value">{stats.total}</div><div className="prog-stat-label">Total Programs</div></div>
        </div>
        <div className="prog-stat-card">
          <div className="prog-stat-icon" style={{ background: "#f0fdf4", color: "#16a34a" }}><Building2 size={22} /></div>
          <div><div className="prog-stat-value">{stats.faculties}</div><div className="prog-stat-label">Faculties</div></div>
        </div>
        <div className="prog-stat-card">
          <div className="prog-stat-icon" style={{ background: "#f0f9ff", color: "#0284c7" }}><Search size={22} /></div>
          <div><div className="prog-stat-value">{stats.shown}</div><div className="prog-stat-label">Matching Filter</div></div>
        </div>
        <div className="prog-stat-card">
          <div className="prog-stat-icon" style={{ background: "#fffbeb", color: "#d97706" }}><Layers size={22} /></div>
          <div><div className="prog-stat-value">{stats.orphaned}</div><div className="prog-stat-label">Unassigned</div></div>
        </div>
      </div>

      {/* Toolbar */}
      <div className="prog-toolbar">
        <div className="prog-search-bar" style={{ margin: 0, flex: 1 }}>
          <Search size={16} />
          <input
            type="text"
            placeholder="Search programs by name or faculty…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          {search && <button style={{ background: "none", border: "none", cursor: "pointer", color: "#9ca3af", display: "flex" }} onClick={() => setSearch("")}><X size={14} /></button>}
        </div>
        <div className="prog-filter-group">
          <Building2 size={15} />
          <select className="form-select" style={{ width: "auto", minWidth: 180 }} value={facultyFilter} onChange={(e) => setFacultyFilter(e.target.value)} aria-label="Filter by parent node">
            <option value="">All parents</option>
            {parentOptions.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
          </select>
        </div>
        {(search || facultyFilter) && (
          <button className="btn btn-ghost" onClick={() => { setSearch(""); setFacultyFilter(""); }}>
            <X size={14} /> Clear
          </button>
        )}
      </div>

      <DataTable
        columns={columns}
        data={filtered}
        loading={loading}
        emptyIcon={GraduationCap}
        emptyTitle={search || facultyFilter ? "No matching programs" : "No programs yet"}
        emptyMessage={search || facultyFilter ? "Try adjusting your search or faculty filter." : "Add your first degree program to get started."}
        emptyActionLabel="Add Program"
        emptyAction={openCreate}
        rowKey="id"
        selectedIds={selectedIds}
        onSelectAll={toggleSelectAll}
        onSelectOne={toggleSelectOne}
        compact={compact}
        onCompactToggle={() => setCompact((c) => !c)}
        tableLabel="Academic programs"
      />

      {/* Bulk bar */}
      {selectedIds.size > 0 && (
        <div className="bulk-bar">
          <span>{selectedIds.size} selected</span>
          <button onClick={() => setSelectedIds(new Set())}>Clear</button>
          <PermissionGate resource="programs.programs" minLevel={5}>
            <button className="bulk-danger" onClick={handleBulkDelete} disabled={bulkDeleting}>
              <Trash2 size={13} /> Delete Selected
            </button>
          </PermissionGate>
        </div>
      )}

      {/* Create/Edit Drawer */}
      <Drawer
        open={!!drawer}
        onClose={closeDrawer}
        title={drawer === "create" ? "New Program" : "Edit Program"}
        width={440}
        loading={saving}
        footer={
          <>
            <button className="btn btn-ghost" onClick={closeDrawer}>Cancel</button>
            <button className="btn btn-primary" onClick={handleSave} disabled={saving}>
              {saving ? "Saving…" : drawer === "create" ? "Create" : "Save Changes"}
            </button>
          </>
        }
      >
        {formError && (
          <div role="alert" style={{ padding: "8px 12px", background: "#fef2f2", border: "1px solid #fecaca", borderRadius: 8, color: "#b91c1c", fontSize: 12, marginBottom: 16 }}>
            {formError}
          </div>
        )}
        <form onSubmit={handleSave}>
          <div className="form-group">
            <label htmlFor="prog-name-en">Program Name (English) *</label>
            <input id="prog-name-en" className="form-input" type="text" value={form.nameEn} autoFocus
              onChange={(e) => setForm((p) => ({ ...p, nameEn: e.target.value }))}
              placeholder="e.g. Computer Science" />
          </div>
          <div className="form-group">
            <label htmlFor="prog-name-ar">Program Name (Arabic)</label>
            <input id="prog-name-ar" className="form-input" type="text" dir="rtl" value={form.nameAr}
              onChange={(e) => setForm((p) => ({ ...p, nameAr: e.target.value }))}
              placeholder="مثال: علوم الحاسب" />
          </div>
          <div className="form-group">
            <label htmlFor="prog-faculty">Parent Node</label>
            <select id="prog-faculty" className="form-select" value={form.facultyId}
              onChange={(e) => setForm((p) => ({ ...p, facultyId: e.target.value }))}>
              <option value="">Unassigned</option>
              <optgroup label="Faculties">
                {faculties.map((f) => <option key={f.id} value={f.id}>{nodeName(f)}</option>)}
              </optgroup>
              {parentNodes.length > 0 && (
                <optgroup label="Other parent nodes">
                  {parentNodes.filter((d) => !faculties.some((f) => f.id === d.id)).map((d) => (
                    <option key={d.id} value={d.id}>{nodeName(d)}</option>
                  ))}
                </optgroup>
              )}
            </select>
            <small style={{ color: "#9ca3af", fontSize: 11 }}>Programs sit under a faculty or department in the university structure.</small>
          </div>
        </form>
      </Drawer>

      <ConfirmDialog
        open={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleDelete}
        title="Delete Program"
        message={`Delete ${nodeName(deleteTarget)}?`}
        detail="This removes the program from the university structure. This cannot be undone."
        confirmLabel="Delete"
        variant="danger"
      />
    </div>
  );
}

export default ProgramsPage;
