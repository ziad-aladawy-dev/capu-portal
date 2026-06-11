import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Save, ChevronRight, CheckCircle, Trash2, Settings, Shield, GitBranch } from "lucide-react";
import { useServices } from "../../hooks/useServices";
import { getAcademicYears, getSemestersByYear, getServiceById } from "../../services/studentServicesService";
import WorkflowBuilder from "../../components/WorkflowBuilder";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import { ScopeTreeModal } from "../../../../core/components/ScopeTreeModal";
import "../../styles/admin/ServiceBuilder.css";

const fieldTypeNumberToText = {
  1: "Text",
  2: "TextArea",
  3: "Number",
  4: "Date",
  5: "Select",
  6: "MultiSelect",
  7: "File",
  8: "Checkbox",
  9: "Radio"
};

const serviceTypes = [{ value: "General", label: "عامة" }, { value: "Specialized", label: "متخصصة" }, { value: "Administrative", label: "إدارية" }];

const getTypeString = (typeValue) => { if (typeof typeValue === 'string') return typeValue; const map = { 1: "General", 2: "Specialized", 3: "Administrative" }; return map[typeValue] || "General"; };
const getTypeNumber = (typeString) => { const map = { "General": 1, "Specialized": 2, "Administrative": 3 }; return map[typeString] || 1; };

const BasicInfo = ({ t, formData, updateField }) => (
  <><div className="sb-card-header"><div className="sb-card-icon"><Settings size={16} /></div><div><p className="sb-card-title">{t("basic_info")}</p><p className="sb-card-subtitle">{t("basic_info_desc") || "Name, type and description"}</p></div></div>
  <div className="sb-card-body"><div className="sb-form-grid"><div className="sb-form-group span2"><label>{t("service_name")} *</label><input value={formData.name} onChange={e => updateField("name", e.target.value)} /></div>
  <div className="sb-form-group"><label>{t("service_type")} *</label><div className="sb-select-wrap"><select value={formData.type} onChange={e => updateField("type", e.target.value)}>{serviceTypes.map(st => <option key={st.value} value={st.value}>{st.label}</option>)}</select><ChevronRight size={14} className="sb-select-arrow" /></div></div>
  <div className="sb-form-group full"><label>{t("description")}</label><textarea rows="3" value={formData.description} onChange={e => updateField("description", e.target.value)} /></div></div></div></>
);

const EligibilityPricing = ({ t, formData, updateField, academicYears, semesters, handleYearChange, handleNodeSelect, removeScopeNode, selectedScopeNodeNames, setShowScopeTree }) => (
  <><div className="sb-card-header"><div className="sb-card-icon"><Shield size={16} /></div><div><p className="sb-card-title">{t("eligibility_pricing")}</p><p className="sb-card-subtitle">{t("eligibility_desc") || "Scope, year, semester and pricing"}</p></div></div>
  <div className="sb-card-body"><div className="sb-form-grid">
    <div className="sb-form-group full"><label>{t("structural_scope")}</label><div className="scope-selector"><button type="button" onClick={() => setShowScopeTree(true)} className="btn-select-node">+ {t("select_structure_nodes")}</button>
      {formData.scopeNodeIds.length > 0 && (<div className="scope-chips">{formData.scopeNodeIds.map((nodeId, idx) => (<span key={nodeId} className="scope-chip">{selectedScopeNodeNames[idx] || nodeId}<button onClick={() => removeScopeNode(nodeId, selectedScopeNodeNames[idx])}><Trash2 size={11} /></button></span>))}</div>)}
      <label className="sb-custom-checkbox"><input type="checkbox" checked={formData.includeDescendants} onChange={e => updateField("includeDescendants", e.target.checked)} /><span className="sb-cb-box"><CheckCircle size={11} /></span><span className="sb-cb-text">{t("include_descendants")}</span></label></div></div>
    <div className="sb-form-group"><label>{t("academic_year")}</label><div className="sb-select-wrap"><select value={formData.academicYearId} onChange={e => handleYearChange(e.target.value)}><option value="">{t("all_years")}</option>{academicYears.map(y => <option key={y.id} value={y.id}>{y.name}</option>)}</select><ChevronRight size={14} className="sb-select-arrow" /></div></div>
    {formData.academicYearId && (<div className="sb-form-group"><label>{t("semester")}</label><div className="sb-select-wrap"><select value={formData.semesterId} onChange={e => updateField("semesterId", e.target.value)}><option value="">{t("all_semesters")}</option>{semesters.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select><ChevronRight size={14} className="sb-select-arrow" /></div></div>)}
    <div className="sb-form-group"><label>{t("level_filter")}</label><div className="sb-select-wrap"><select value={formData.levelOrder || ""} onChange={e => updateField("levelOrder", e.target.value ? parseInt(e.target.value) : null)}><option value="">{t("all_levels")}</option>{[1,2,3,4].map(l => <option key={l} value={l}>{t(`level_${l}`)}</option>)}</select><ChevronRight size={14} className="sb-select-arrow" /></div></div>
    <div className="sb-form-group"><label>&nbsp;</label><label className="sb-custom-checkbox" style={{height:36,alignItems:"center"}}><input type="checkbox" checked={formData.isPaid} onChange={e => updateField("isPaid", e.target.checked)} /><span className="sb-cb-box"><CheckCircle size={11} /></span><span className="sb-cb-text">{t("paid_service")}</span></label></div>
    {formData.isPaid && (<div className="sb-form-group"><label>{t("price")}</label><input type="number" step="0.01" min="0" value={formData.price} onChange={e => updateField("price", parseFloat(e.target.value))} /></div>)}
  </div></div></>
);

const WorkflowStep = ({ t, formData, updateField }) => (
  <><div className="sb-card-header"><div className="sb-card-icon"><GitBranch size={16} /></div><div><p className="sb-card-title">{t("workflow_builder")}</p><p className="sb-card-subtitle">{t("workflow_desc") || "Define the service workflow steps"}</p></div></div>
  <div className="sb-card-body"><WorkflowBuilder workflow={formData.workflow} onChange={newWorkflow => updateField("workflow", newWorkflow)} /></div></>
);

const ServiceBuilder = () => {
  const { id } = useParams(); const navigate = useNavigate(); const { t } = useTranslation(); const { addService, editService } = useServices();
  const [step, setStep] = useState(1); const [saving, setSaving] = useState(false); const [loading, setLoading] = useState(!!id); const [error, setError] = useState(null);
  const [formData, setFormData] = useState({ name: "", type: "General", description: "", isPaid: false, price: 0, scopeNodeIds: [], includeDescendants: true, academicYearId: "", semesterId: "", levelOrder: null, workflow: { steps: [] } });
  const [academicYears, setAcademicYears] = useState([]); const [semesters, setSemesters] = useState([]); const [showScopeTree, setShowScopeTree] = useState(false); const [selectedScopeNodeNames, setSelectedScopeNodeNames] = useState([]);
  useEffect(() => { loadAcademicYears(); if (id) loadService(); }, [id]);
  const loadAcademicYears = async () => { try { setAcademicYears(await getAcademicYears()); } catch (e) {} };
  const loadSemesters = async (yearId) => { if (!yearId) { setSemesters([]); return; } try { setSemesters(await getSemestersByYear(yearId)); } catch (e) {} };
  
  const loadService = async () => {
    setLoading(true);
    try {
      const data = await getServiceById(id);
      const nodeNames = data.scopeNodesDetails?.map(node => node.localizedName || node.name) || [];
      setSelectedScopeNodeNames(nodeNames);

      // تحويل workflow: تحويل fieldType من رقم إلى النص المناسب، وتعيين required من isRequired
      const workflow = data.workflow ? {
        ...data.workflow,
        steps: data.workflow.steps?.map(step => ({
          ...step,
          fields: step.fields?.map(field => ({
            id: field.id,
            type: fieldTypeNumberToText[field.fieldType] || "Text",
            label: field.label,
            required: field.isRequired,
            options: field.options || []
          })) || []
        })) || []
      } : { steps: [] };

      setFormData({
        name: data.name || "",
        type: getTypeString(data.type),
        description: data.description || "",
        isPaid: data.isPaid || false,
        price: data.price || 0,
        scopeNodeIds: data.scopeNodeIds || [],
        includeDescendants: data.includeDescendants !== undefined ? data.includeDescendants : true,
        academicYearId: data.academicYearId || "",
        semesterId: data.semesterId || "",
        levelOrder: data.levelOrder || null,
        workflow: workflow
      });
      if (data.academicYearId) await loadSemesters(data.academicYearId);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };
  
  const updateField = (field, value) => setFormData(prev => ({ ...prev, [field]: value }));
  const handleYearChange = async (yearId) => { updateField("academicYearId", yearId); updateField("semesterId", ""); await loadSemesters(yearId); };
  const handleNodeSelect = (node) => { if (!formData.scopeNodeIds.includes(node.id)) { setFormData(prev => ({ ...prev, scopeNodeIds: [...prev.scopeNodeIds, node.id] })); setSelectedScopeNodeNames(prev => [...prev, node.localizedName || node.name]); } setShowScopeTree(false); };
  const removeScopeNode = (nodeId, nodeName) => { setFormData(prev => ({ ...prev, scopeNodeIds: prev.scopeNodeIds.filter(i => i !== nodeId) })); setSelectedScopeNodeNames(prev => prev.filter(name => name !== nodeName)); };
  const cleanWorkflowForApi = (workflow) => { if (!workflow?.steps) return { name: "", steps: [] }; const stepTypeMap = { Form: 1, Review: 2, Payment: 3 }; const fieldTypeMap = { Text:1, TextArea:2, Number:3, Date:4, Select:5, MultiSelect:6, File:7, Checkbox:8, Radio:9 }; return { name: "", steps: workflow.steps.map(({ id: stepId, ...step }) => ({ ...step, stepType: typeof step.stepType === "string" ? (stepTypeMap[step.stepType] || 1) : step.stepType, fields: (step.fields || []).map(({ id: fieldId, ...field }) => ({ ...field, fieldType: typeof field.type === "string" ? (fieldTypeMap[field.type] || 1) : field.fieldType, isRequired: field.required || false, options: field.options || [] })) })) }; };
  const validateStep = (currentStep = step) => { if (currentStep===1 && !formData.name.trim()) { setError(t("sb_name_required")); return false; } if (currentStep===2 && formData.scopeNodeIds.length===0) { setError(t("sb_scope_required")); return false; } if (currentStep===2 && formData.isPaid && formData.price<=0) { setError(t("sb_price_positive")); return false; } if (currentStep===3 && (!formData.workflow?.steps?.length)) { setError(t("sb_workflow_required")); return false; } return true; };
  const handleNext = () => { if (validateStep()) { setError(null); setStep(s => Math.min(3, s+1)); } };
  const handleSave = async () => { setSaving(true); setError(null); try { const typeMap = { General:1, Specialized:2, Administrative:3 }; const payload = { name: formData.name, type: typeof formData.type === "string" ? (typeMap[formData.type]||1) : formData.type, description: formData.description, isPaid: formData.isPaid, price: formData.price, scopeNodeIds: formData.scopeNodeIds, includeDescendants: formData.includeDescendants, academicYearId: formData.academicYearId || null, semesterId: formData.semesterId || null, levelOrder: formData.levelOrder, workflow: cleanWorkflowForApi(formData.workflow) }; if (id) await editService(id, payload); else await addService(payload); navigate("/admin/student-services/services"); } catch (e) { setError(e.response?.data?.title || e.response?.data?.message || e.message || "Failed to save"); } finally { setSaving(false); } };
  if (loading) return <LoadingSpinner />;
  return (
    <div className="service-builder-container">
      <div className="sb-header"><div className="sb-header-icon"><Settings size={16} /></div><div className="sb-header-text"><h1>{id ? t("edit_service") : t("create_service")}</h1><p>{id ? t("edit_service_desc") || "Update service details" : t("create_service_desc") || "Fill in the details to create a new service"}</p></div></div>
      {error && <div className="sb-error">{error}</div>}
      <div className="sb-stepper">{["Basic Info", "Eligibility & Pricing", "Workflow Builder"].map((s,idx) => (<div key={idx} className={`sb-step ${step===idx+1?"active":step>idx+1?"done":""}`} onClick={() => { if(idx+1<=step) setStep(idx+1); }}><div className="sb-step-number">{step>idx+1?<CheckCircle size={13} />:idx+1}</div><span>{t(s.toLowerCase().replace(/ /g,'_')) || s}</span>{idx<2 && <ChevronRight size={13} className="sb-step-arrow" />}</div>))}</div>
      <div className="sb-step-content">{step===1 && <BasicInfo t={t} formData={formData} updateField={updateField} />}{step===2 && <EligibilityPricing t={t} formData={formData} updateField={updateField} academicYears={academicYears} semesters={semesters} handleYearChange={handleYearChange} handleNodeSelect={handleNodeSelect} removeScopeNode={removeScopeNode} selectedScopeNodeNames={selectedScopeNodeNames} setShowScopeTree={setShowScopeTree} />}{step===3 && <WorkflowStep t={t} formData={formData} updateField={updateField} />}</div>
      <div className="sb-actions"><button className="sb-btn secondary" onClick={() => setStep(s => Math.max(1,s-1))} disabled={step===1}>{t("previous")}</button>{step<3 ? <button className="sb-btn primary" onClick={handleNext}>{t("next")} <ChevronRight size={14} /></button> : <button className="sb-btn primary" onClick={handleSave} disabled={saving}>{saving ? t("saving") : t("submit")}</button>}</div>
      <ScopeTreeModal isOpen={showScopeTree} onClose={() => setShowScopeTree(false)} onSelect={handleNodeSelect} />
    </div>
  );
};

export default ServiceBuilder;