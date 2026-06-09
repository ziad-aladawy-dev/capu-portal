import { useState, useEffect, useCallback } from "react";
import { BookOpen, Plus, Pencil, Trash2, Search, AlertCircle } from "lucide-react";
import * as structureService from "../../../core/services/structureService";
import PermissionGate from "../../../core/auth/PermissionGate";
import "../styles/programs.css";

function ProgramsPage() {
  const [programs, setPrograms] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState(null);
  const [formData, setFormData] = useState({ nameEn: "", nameAr: "", facultyId: "" });
  const [faculties, setFaculties] = useState([]);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [progs, facs] = await Promise.all([
        structureService.fetchPrograms(),
        structureService.fetchFaculties(),
      ]);
      setPrograms(Array.isArray(progs) ? progs : []);
      setFaculties(Array.isArray(facs) ? facs : []);
    } catch (err) {
      setError(err.message || "Failed to load programs");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const handleSave = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      const payload = {
        name: { en: formData.nameEn, ar: formData.nameAr },
        parentId: formData.facultyId || null,
        type: "Program",
      };
      if (editing) {
        await structureService.updateStructureNode(editing.id, payload);
      } else {
        await structureService.createStructureNode(payload);
      }
      setShowForm(false);
      setEditing(null);
      setFormData({ nameEn: "", nameAr: "", facultyId: "" });
      await load();
    } catch (err) {
      setError(err.message || "Failed to save program");
    } finally {
      setSaving(false);
    }
  };

  const handleEdit = (program) => {
    setEditing(program);
    setFormData({
      nameEn: program.name?.en || program.name || "",
      nameAr: program.name?.ar || "",
      facultyId: program.parentId || "",
    });
    setShowForm(true);
  };

  const handleDelete = async (id) => {
    if (!window.confirm("Delete this program? This action cannot be undone.")) return;
    try {
      await structureService.deleteStructureNode(id);
      await load();
    } catch (err) {
      setError(err.message || "Failed to delete program");
    }
  };

  const filtered = programs.filter((p) => {
    const q = search.toLowerCase();
    const name = p.name?.en?.toLowerCase() + " " + p.name?.ar?.toLowerCase();
    return name.includes(q);
  });

  const getFacultyName = (facultyId) => {
    const f = faculties.find((x) => x.id === facultyId);
    return f?.name?.en || f?.name || "—";
  };

  return (
    <div className="prog-page">
      <div className="prog-header">
        <div className="prog-header-left">
          <BookOpen size={24} />
          <div>
            <h1>Academic Programs</h1>
            <p>Manage degree programs offered by the university</p>
          </div>
        </div>
        <PermissionGate resource="programs.programs" minLevel={2}>
          <button className="btn btn-primary" onClick={() => { setEditing(null); setFormData({ nameEn: "", nameAr: "", facultyId: "" }); setShowForm(!showForm); }}>
            <Plus size={16} /> {showForm ? "Cancel" : "Add Program"}
          </button>
        </PermissionGate>
      </div>

      {error && (
        <div className="prog-alert prog-alert-error">
          <AlertCircle size={16} /> {error}
          <button onClick={() => setError(null)}>&times;</button>
        </div>
      )}

      {showForm && (
        <form onSubmit={handleSave} className="prog-form-card">
          <h3>{editing ? "Edit Program" : "New Program"}</h3>
          <div className="prog-form-row">
            <div className="form-group">
              <label>Name (English)</label>
              <input type="text" className="form-input" required value={formData.nameEn} onChange={(e) => setFormData({ ...formData, nameEn: e.target.value })} />
            </div>
            <div className="form-group">
              <label>Name (Arabic)</label>
              <input type="text" className="form-input" value={formData.nameAr} onChange={(e) => setFormData({ ...formData, nameAr: e.target.value })} />
            </div>
          </div>
          <div className="form-group">
            <label>Faculty</label>
            <select className="form-select" value={formData.facultyId} onChange={(e) => setFormData({ ...formData, facultyId: e.target.value })}>
              <option value="">Select Faculty</option>
              {faculties.map((f) => (
                <option key={f.id} value={f.id}>{f.name?.en || f.name}</option>
              ))}
            </select>
          </div>
          <div className="form-actions">
            <button type="submit" className="btn btn-primary" disabled={saving}>
              {saving ? "Saving..." : (editing ? "Update" : "Create")}
            </button>
            <button type="button" className="btn btn-ghost" onClick={() => setShowForm(false)}>Cancel</button>
          </div>
        </form>
      )}

      <div className="prog-search-bar">
        <Search size={16} />
        <input type="text" placeholder="Search programs..." value={search} onChange={(e) => setSearch(e.target.value)} />
      </div>

      {loading ? (
        <div className="prog-loading">Loading programs...</div>
      ) : filtered.length === 0 ? (
        <div className="prog-empty">
          <BookOpen size={48} />
          <h3>{search ? "No programs match your search" : "No programs yet"}</h3>
          <p>{search ? "Try a different search term" : "Add your first academic program to get started"}</p>
        </div>
      ) : (
        <div className="prog-table-wrapper">
          <table>
            <thead>
              <tr>
                <th>Name (EN)</th>
                <th>Name (AR)</th>
                <th>Faculty</th>
                <th className="text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((p) => (
                <tr key={p.id}>
                  <td>{p.name?.en || p.name}</td>
                  <td>{p.name?.ar || "—"}</td>
                  <td>{getFacultyName(p.parentId)}</td>
                  <td className="text-right">
                    <div className="prog-action-buttons">
                      <PermissionGate resource="programs.programs" minLevel={3}>
                        <button className="btn btn-icon" title="Edit" onClick={() => handleEdit(p)}>
                          <Pencil size={14} />
                        </button>
                      </PermissionGate>
                      <PermissionGate resource="programs.programs" minLevel={5}>
                        <button className="btn btn-icon btn-icon-danger" title="Delete" onClick={() => handleDelete(p.id)}>
                          <Trash2 size={14} />
                        </button>
                      </PermissionGate>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export default ProgramsPage;
