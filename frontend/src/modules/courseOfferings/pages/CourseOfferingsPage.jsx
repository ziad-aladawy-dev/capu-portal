import { useState, useCallback, useEffect, useMemo } from "react";
import {
  CalendarCheck, Plus, Edit2, AlertTriangle, Search, X,
} from "lucide-react";
import { useDomain } from "../../../core/contexts/DomainContext";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import { OFFERING_STATUS_LABELS, REGISTRATION_STATE_LABELS } from "../../../core/services/courseOfferingService";
import { useCourseOfferings } from "../hooks/useCourseOfferings";
import OfferingForm from "../components/OfferingForm";
import "../styles/courseOfferings.css";

function CourseOfferingsPage() {
  const { scopeNode } = useDomain();
  const { selectedSemesterObj } = useAcademic();

  const {
    offerings, loading, error, courses, faculties, semesterId,
    loadOfferings, createOffering, updateOffering, setError,
  } = useCourseOfferings();

  const [structureNodeId, setStructureNodeId] = useState(null);
  const [statusFilter, setStatusFilter] = useState("");
  const [search, setSearch] = useState("");

  const [showForm, setShowForm] = useState(false);
  const [editOffering, setEditOffering] = useState(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

  useEffect(() => {
    if (scopeNode?.id) setStructureNodeId(scopeNode.id);
  }, [scopeNode]);

  useEffect(() => {
    if (structureNodeId && semesterId) {
      const status = statusFilter !== "" ? Number(statusFilter) : undefined;
      loadOfferings(structureNodeId, semesterId, status);
    }
  }, [structureNodeId, semesterId, statusFilter, loadOfferings]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return offerings;
    return offerings.filter((o) => {
      const course = courses.find((c) => c.id === o.courseId);
      const code = course?.code || "";
      const title = course?.title || "";
      return code.toLowerCase().includes(q) || title.toLowerCase().includes(q) || o.sectionCode.toLowerCase().includes(q);
    });
  }, [offerings, search, courses]);

  const getCourseInfo = (courseId) => courses.find((c) => c.id === courseId);

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
        await updateOffering(editOffering.id, body);
      } else {
        await createOffering({
          courseId: formData.courseId,
          semesterId: formData.semesterId,
          structureNodeId: formData.structureNodeId,
          sectionCode: formData.sectionCode,
          capacity: formData.capacity,
          status: formData.status,
          registrationState: formData.registrationState,
        });
      }
      closeForm();
      loadOfferings(structureNodeId, semesterId, statusFilter !== "" ? Number(statusFilter) : undefined);
    } catch (err) {
      setFormError(err.message || "Failed to save offering");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="co-page">
      <div className="co-header">
        <div className="co-header-left">
          <CalendarCheck size={20} />
          <div>
            <h1>Course Offerings</h1>
            <p>Manage course sections per term and structure node</p>
          </div>
        </div>
        <button className="co-btn co-btn-primary" onClick={openCreate}>
          <Plus size={16} /> New Offering
        </button>
      </div>

      <div className="co-toolbar">
        <div className="co-search">
          <Search size={14} />
          <input
            type="text"
            placeholder="Search by course code, title or section..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          {search && (
            <button className="co-search-clear" onClick={() => setSearch("")}>
              <X size={14} />
            </button>
          )}
        </div>
        <select
          className="co-select"
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
        >
          <option value="">All Statuses</option>
          {Object.entries(OFFERING_STATUS_LABELS).map(([val, label]) => (
            <option key={val} value={val}>{label}</option>
          ))}
        </select>
      </div>

      {error && (
        <div className="co-alert co-alert-error">
          <AlertTriangle size={16} /> {error}
        </div>
      )}

      <div className="co-table-wrap">
        <table className="co-table">
          <thead>
            <tr>
              <th>Course</th>
              <th>Section</th>
              <th>Capacity</th>
              <th>Enrolled</th>
              <th>Status</th>
              <th>Registration</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr><td colSpan={7} className="co-table-empty">Loading...</td></tr>
            ) : filtered.length === 0 ? (
              <tr><td colSpan={7} className="co-table-empty">No course offerings found</td></tr>
            ) : (
              filtered.map((offering) => {
                const course = getCourseInfo(offering.courseId);
                const statusLabel = OFFERING_STATUS_LABELS[offering.status] || "Unknown";
                const regLabel = REGISTRATION_STATE_LABELS[offering.registrationState] || "Unknown";
                const filled = offering.capacity > 0 ? Math.round((offering.registeredCount / offering.capacity) * 100) : 0;

                return (
                  <tr key={offering.id}>
                    <td>
                      <div className="co-course-info">
                        <span className="co-course-code">{course?.code || "—"}</span>
                        <span className="co-course-title">{course?.title || "Unknown Course"}</span>
                      </div>
                    </td>
                    <td><span className="co-section-badge">{offering.sectionCode}</span></td>
                    <td>{offering.capacity}</td>
                    <td>
                      <div className="co-enrolled-cell">
                        <span>{offering.registeredCount}</span>
                        <div className="co-capacity-bar">
                          <div
                            className={`co-capacity-fill ${filled >= 90 ? "full" : filled >= 70 ? "warn" : "ok"}`}
                            style={{ width: `${Math.min(filled, 100)}%` }}
                          />
                        </div>
                      </div>
                    </td>
                    <td>
                      <span className={`co-status-badge status-${statusLabel.toLowerCase()}`}>
                        {statusLabel}
                      </span>
                    </td>
                    <td>
                      <span className={`co-reg-badge reg-${regLabel.toLowerCase()}`}>
                        {regLabel}
                      </span>
                    </td>
                    <td>
                      <button className="co-action-btn" onClick={() => openEdit(offering)} title="Edit">
                        <Edit2 size={14} />
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {showForm && (
        <OfferingForm
          editOffering={editOffering}
          courses={courses}
          faculties={faculties}
          semesterId={semesterId}
          structureNodeId={structureNodeId}
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
