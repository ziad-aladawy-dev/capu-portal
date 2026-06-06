import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ArrowLeft, Save, ChevronRight, CheckCircle, Plus, Trash2 } from "lucide-react";
import { useServices } from "../../hooks/useServices";
import { getAcademicYears, getFaculties } from "../../services/studentServicesService";
import WorkflowBuilder from "../../components/WorkflowBuilder";
import LoadingSpinner from "../../components/LoadingSpinner";
import "../../styles/admin/ServiceBuilder.css";

const serviceTypes = [
  { value: "General", label: "عامة" },
  { value: "Specialized", label: "متخصصة" },
  { value: "Administrative", label: "إدارية" },
];

const ServiceBuilder = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { addService, editService, loading: serviceLoading } = useServices();
  
  const [step, setStep] = useState(1);
  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(!!id);
  
  const [formData, setFormData] = useState({
    name: "",
    type: "General",
    description: "",
    isPaid: false,
    price: 0,
    scopeNodeIds: [],
    includeDescendants: true,
    academicYearId: "",
    workflow: { steps: [] },
  });
  
  const [faculties, setFaculties] = useState([]);
  const [selectedFacultyId, setSelectedFacultyId] = useState("");
  const [academicYears, setAcademicYears] = useState([]);
  
  useEffect(() => {
    loadFaculties();
    loadAcademicYears();
    if (id) loadService();
  }, [id]);
  
  const loadFaculties = async () => {
    try {
      const data = await getFaculties();
      setFaculties(data);
    } catch (err) {
      console.error("Failed to load faculties", err);
    }
  };
  
  const loadAcademicYears = async () => {
    try {
      const data = await getAcademicYears();
      setAcademicYears(data);
    } catch (err) {
      console.error("Failed to load academic years", err);
    }
  };
  
  const loadService = async () => {
    setLoading(true);
    try {
      const { getServiceById } = await import("../../services/studentServicesService");
      const data = await getServiceById(id);
      setFormData({
        name: data.name || "",
        type: data.type || "General",
        description: data.description || "",
        isPaid: data.isPaid || false,
        price: data.price || 0,
        scopeNodeIds: data.scopeNodeIds || [],
        includeDescendants: data.includeDescendants !== undefined ? data.includeDescendants : true,
        academicYearId: data.academicYearId || "",
        workflow: data.workflow || { steps: [] },
      });
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };
  
  const updateField = (field, value) => setFormData((prev) => ({ ...prev, [field]: value }));
  
  const addScopeNode = (nodeId) => {
    if (!formData.scopeNodeIds.includes(nodeId)) {
      setFormData((prev) => ({
        ...prev,
        scopeNodeIds: [...prev.scopeNodeIds, nodeId],
      }));
    }
    setSelectedFacultyId("");
  };
  
  const removeScopeNode = (nodeId) => {
    setFormData((prev) => ({
      ...prev,
      scopeNodeIds: prev.scopeNodeIds.filter((id) => id !== nodeId),
    }));
  };
  
  const handleSave = async () => {
    setSaving(true);
    try {
      const payload = {
        name: formData.name,
        type: formData.type,
        description: formData.description,
        isPaid: formData.isPaid,
        price: formData.price,
        scopeNodeIds: formData.scopeNodeIds,
        includeDescendants: formData.includeDescendants,
        academicYearId: formData.academicYearId || null,
        workflow: formData.workflow,
      };
      if (id) await editService(id, payload);
      else await addService(payload);
      navigate("/admin/student-services/services");
    } catch (err) {
      console.error(err);
      alert(err.message);
    } finally {
      setSaving(false);
    }
  };
  
  const BasicInfoComponent = () => (
    <div className="sb-form-grid">
      <div className="sb-form-group">
        <label>{t("service_name")} *</label>
        <input value={formData.name} onChange={(e) => updateField("name", e.target.value)} />
      </div>
      <div className="sb-form-group">
        <label>{t("service_type")} *</label>
        <select value={formData.type} onChange={(e) => updateField("type", e.target.value)}>
          {serviceTypes.map((t) => (
            <option key={t.value} value={t.value}>{t.label}</option>
          ))}
        </select>
      </div>
      <div className="sb-form-group full">
        <label>{t("description")}</label>
        <textarea rows="3" value={formData.description} onChange={(e) => updateField("description", e.target.value)} />
      </div>
    </div>
  );
  
  const EligibilityPricingComponent = () => (
    <div className="sb-form-grid">
      <div className="sb-form-group full">
        <label>{t("structural_scope")}</label>
        <div className="scope-selector">
          <div className="scope-node-selector">
            <select value={selectedFacultyId} onChange={(e) => setSelectedFacultyId(e.target.value)}>
              <option value="">{t("select_faculty")}</option>
              {faculties.map((f) => (
                <option key={f.id} value={f.id}>{f.localizedName || f.name}</option>
              ))}
            </select>
            <button type="button" onClick={() => addScopeNode(selectedFacultyId)} disabled={!selectedFacultyId}>
              <Plus size={14} /> {t("add")}
            </button>
          </div>
          <div className="scope-chips">
            {formData.scopeNodeIds.map((nodeId) => {
              const node = faculties.find((f) => f.id === nodeId);
              return (
                <span key={nodeId} className="scope-chip">
                  {node?.localizedName || node?.name}
                  <button type="button" onClick={() => removeScopeNode(nodeId)}>
                    <Trash2 size={12} />
                  </button>
                </span>
              );
            })}
          </div>
          <label className="sb-checkbox-label">
            <input
              type="checkbox"
              checked={formData.includeDescendants}
              onChange={(e) => updateField("includeDescendants", e.target.checked)}
            />
            {t("include_descendants")}
          </label>
        </div>
      </div>
      <div className="sb-form-group full">
        <label>{t("academic_year")}</label>
        <select value={formData.academicYearId} onChange={(e) => updateField("academicYearId", e.target.value)}>
          <option value="">{t("all_years")}</option>
          {academicYears.map((y) => (
            <option key={y.id} value={y.id}>{y.name}</option>
          ))}
        </select>
      </div>
      <div className="sb-form-group">
        <label className="sb-checkbox-label">
          <input type="checkbox" checked={formData.isPaid} onChange={(e) => updateField("isPaid", e.target.checked)} />
          {t("paid")}
        </label>
      </div>
      {formData.isPaid && (
        <div className="sb-form-group">
          <label>{t("price")}</label>
          <input type="number" step="0.01" value={formData.price} onChange={(e) => updateField("price", parseFloat(e.target.value))} />
        </div>
      )}
    </div>
  );
  
  const steps = [
    { title: t("basic_info"), component: BasicInfoComponent },
    { title: t("eligibility_pricing"), component: EligibilityPricingComponent },
    { title: t("workflow_builder"), component: () => (
      <WorkflowBuilder workflow={formData.workflow} onChange={(newWorkflow) => updateField("workflow", newWorkflow)} />
    ) },
  ];
  
  if (loading) return <LoadingSpinner />;
  
  return (
    <div className="service-builder-container">
      <div className="sb-header">
        <button className="sb-back-btn" onClick={() => navigate(-1)}>
          <ArrowLeft size={18} />
        </button>
        <h1>{id ? t("edit_service") : t("create_service")}</h1>
        <button className="sb-save-btn" onClick={handleSave} disabled={saving}>
          <Save size={16} /> {saving ? t("saving") : t("save")}
        </button>
      </div>
      <div className="sb-stepper">
        {steps.map((s, idx) => (
          <div 
            key={idx} 
            className={`sb-step ${step === idx + 1 ? "active" : step > idx + 1 ? "done" : ""}`} 
            onClick={() => setStep(idx + 1)}
          >
            <div className="sb-step-number">
              {step > idx + 1 ? <CheckCircle size={14} /> : idx + 1}
            </div>
            <span>{s.title}</span>
            {idx < steps.length - 1 && <ChevronRight size={14} className="sb-step-arrow" />}
          </div>
        ))}
      </div>
      <div className="sb-step-content">{steps[step - 1].component()}</div>
    </div>
  );
};

export default ServiceBuilder;