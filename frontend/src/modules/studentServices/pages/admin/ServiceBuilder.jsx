import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ChevronRight, CheckCircle, Trash2, Settings, Shield, GitBranch, ArrowLeft } from "lucide-react";
import { useServices } from "../../hooks/useServices";
import { getAcademicYears, getSemestersByYear, getServiceById } from "../../services/studentServicesService";
import WorkflowBuilder from "../../components/WorkflowBuilder";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import PageHeader from "../../../../core/components/PageHeader";
import { ScopeTreeModal } from "../../../../core/components/ScopeTreeModal";
import { useToast } from "../../../../core/components/Toast";
import { SERVICE_TYPE, SERVICE_TYPE_LABELS } from "../../../../core/constants/requestStatus";
import { WORKFLOW_STEP_TYPE, STEP_FIELD_TYPE, STEP_FIELD_TYPE_NAMES } from "../../../../core/constants/workflowTypes";
import "../../styles/admin/ServiceBuilder.css";

const getTypeString = (typeValue) => {
  if (typeof typeValue === "string") return typeValue;
  return SERVICE_TYPE_LABELS[typeValue] || "General";
};
const getTypeNumber = (typeString) => SERVICE_TYPE[typeString] || SERVICE_TYPE.General;

const STEPS = [
  { key: "basic_info" },
  { key: "eligibility_pricing" },
  { key: "workflow_builder" },
];

const BasicInfo = ({ t, formData, updateField, serviceTypes }) => (
  <>
    <div className="sb-card-header">
      <div className="sb-card-icon"><Settings size={16} /></div>
      <div>
        <p className="sb-card-title">{t("basic_info")}</p>
        <p className="sb-card-subtitle">{t("basic_info_desc")}</p>
      </div>
    </div>
    <div className="sb-card-body">
      <div className="sb-form-grid">
        <div className="sb-form-group span2">
          <label>{t("service_name")} *</label>
          <input value={formData.name} onChange={e => updateField("name", e.target.value)} />
        </div>
        <div className="sb-form-group">
          <label>{t("service_type")} *</label>
          <div className="sb-select-wrap">
            <select value={formData.type} onChange={e => updateField("type", e.target.value)}>
              {serviceTypes.map(st => (
                <option key={st.value} value={st.value}>{st.label}</option>
              ))}
            </select>
            <ChevronRight size={14} className="sb-select-arrow" />
          </div>
        </div>
        <div className="sb-form-group full">
          <label>{t("description")}</label>
          <textarea rows="3" value={formData.description} onChange={e => updateField("description", e.target.value)} />
        </div>
      </div>
    </div>
  </>
);

const EligibilityPricing = ({ t, formData, updateField, academicYears, semesters, handleYearChange, removeScopeNode, scopeNodeNames, setShowScopeTree }) => (
  <>
    <div className="sb-card-header">
      <div className="sb-card-icon"><Shield size={16} /></div>
      <div>
        <p className="sb-card-title">{t("eligibility_pricing")}</p>
        <p className="sb-card-subtitle">{t("eligibility_desc")}</p>
      </div>
    </div>
    <div className="sb-card-body">
      <div className="sb-form-grid">
        {/* Structural scope selector */}
        <div className="sb-form-group full">
          <label>{t("structural_scope")}</label>
          <div className="sb-scope-selector">
            <button type="button" onClick={() => setShowScopeTree(true)} className="btn-outline">
              + {t("select_structure_nodes")}
            </button>
            {formData.scopeNodeIds.length > 0 && (
              <div className="sb-scope-chips">
                {formData.scopeNodeIds.map(nodeId => (
                  <span key={nodeId} className="sb-scope-chip">
                    {scopeNodeNames[nodeId] || nodeId}
                    <button onClick={() => removeScopeNode(nodeId)}>
                      <Trash2 size={11} />
                    </button>
                  </span>
                ))}
              </div>
            )}
            <label className="sb-custom-checkbox">
              <input
                type="checkbox"
                checked={formData.includeDescendants}
                onChange={e => updateField("includeDescendants", e.target.checked)}
              />
              <span className="sb-cb-box"><CheckCircle size={11} /></span>
              <span className="sb-cb-text">{t("include_descendants")}</span>
            </label>
          </div>
        </div>

        {/* Academic year */}
        <div className="sb-form-group">
          <label>{t("academic_year")}</label>
          <div className="sb-select-wrap">
            <select value={formData.academicYearId} onChange={e => handleYearChange(e.target.value)}>
              <option value="">{t("all_years")}</option>
              {academicYears.map(y => (
                <option key={y.id} value={y.id}>{y.name}</option>
              ))}
            </select>
            <ChevronRight size={14} className="sb-select-arrow" />
          </div>
        </div>

        {/* Semester */}
        {formData.academicYearId && (
          <div className="sb-form-group">
            <label>{t("semester")}</label>
            <div className="sb-select-wrap">
              <select value={formData.semesterId} onChange={e => updateField("semesterId", e.target.value)}>
                <option value="">{t("all_semesters")}</option>
                {semesters.map(s => (
                  <option key={s.id} value={s.id}>{s.name}</option>
                ))}
              </select>
              <ChevronRight size={14} className="sb-select-arrow" />
            </div>
          </div>
        )}

        {/* Level filter */}
        <div className="sb-form-group">
          <label>{t("level_filter")}</label>
          <div className="sb-select-wrap">
            <select
              value={formData.levelOrder || ""}
              onChange={e => updateField("levelOrder", e.target.value ? parseInt(e.target.value) : null)}
            >
              <option value="">{t("all_levels")}</option>
              {[1, 2, 3, 4].map(l => (
                <option key={l} value={l}>{t(`level_${l}`)}</option>
              ))}
            </select>
            <ChevronRight size={14} className="sb-select-arrow" />
          </div>
        </div>

        {/* Paid service toggle */}
        <div className="sb-form-group">
          <label>&nbsp;</label>
          <label className="sb-custom-checkbox" style={{ height: 38, alignItems: "center" }}>
            <input type="checkbox" checked={formData.isPaid} onChange={e => updateField("isPaid", e.target.checked)} />
            <span className="sb-cb-box"><CheckCircle size={11} /></span>
            <span className="sb-cb-text">{t("paid_service")}</span>
          </label>
        </div>

        {/* Price field */}
        {formData.isPaid && (
          <div className="sb-form-group">
            <label>{t("price")}</label>
            <input
              type="number"
              step="0.01"
              min="0"
              value={formData.price}
              onChange={e => updateField("price", parseFloat(e.target.value))}
            />
          </div>
        )}
      </div>
    </div>
  </>
);

const WorkflowStep = ({ t, formData, updateField }) => (
  <>
    <div className="sb-card-header">
      <div className="sb-card-icon"><GitBranch size={16} /></div>
      <div>
        <p className="sb-card-title">{t("workflow_builder")}</p>
        <p className="sb-card-subtitle">{t("workflow_desc")}</p>
      </div>
    </div>
    <div className="sb-card-body">
      <WorkflowBuilder
        workflow={formData.workflow}
        onChange={newWorkflow => updateField("workflow", newWorkflow)}
      />
    </div>
  </>
);

const ServiceBuilder = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { addService, editService } = useServices();
  const { addToast } = useToast();

  const [step, setStep] = useState(1);
  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(!!id);
  const [error, setError] = useState(null);
  const [formData, setFormData] = useState({
    name: "", type: "General", description: "", isPaid: false, price: 0,
    scopeNodeIds: [], includeDescendants: true, academicYearId: "", semesterId: "",
    levelOrder: null, workflow: { steps: [] }
  });
  const [academicYears, setAcademicYears] = useState([]);
  const [semesters, setSemesters] = useState([]);
  const [showScopeTree, setShowScopeTree] = useState(false);
  const [scopeNodeNames, setScopeNodeNames] = useState({});

  const serviceTypes = [
    { value: "General", label: t("general") },
    { value: "Specialized", label: t("specialized") },
    { value: "Administrative", label: t("administrative") },
  ];

  const loadAcademicYears = async () => {
    try { setAcademicYears(await getAcademicYears()); } catch (e) { console.error(e); }
  };

  const loadSemesters = async (yearId) => {
    if (!yearId) { setSemesters([]); return; }
    try { setSemesters(await getSemestersByYear(yearId)); } catch (e) { console.error(e); }
  };

  const loadService = async () => {
    try {
      const data = await getServiceById(id);
      setScopeNodeNames(Object.fromEntries(
        (data.scopeNodesDetails || []).map(node => [node.id, node.localizedName || node.name])
      ));

      const workflow = data.workflow ? {
        ...data.workflow,
        steps: data.workflow.steps?.map(wfStep => ({
          ...wfStep,
          fields: wfStep.fields?.map(field => ({
            id: field.id,
            type: STEP_FIELD_TYPE_NAMES[field.fieldType] || "Text",
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

  // eslint-disable-next-line react-hooks/set-state-in-effect -- async loader; setState only after await
  useEffect(() => { loadAcademicYears(); if (id) loadService(); }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  const updateField = (field, value) => setFormData(prev => ({ ...prev, [field]: value }));

  const handleYearChange = async (yearId) => {
    updateField("academicYearId", yearId);
    updateField("semesterId", "");
    await loadSemesters(yearId);
  };

  const handleNodeSelect = (node) => {
    if (!formData.scopeNodeIds.includes(node.id)) {
      setFormData(prev => ({ ...prev, scopeNodeIds: [...prev.scopeNodeIds, node.id] }));
      setScopeNodeNames(prev => ({ ...prev, [node.id]: node.localizedName || node.name }));
    }
    setShowScopeTree(false);
  };

  const removeScopeNode = (nodeId) => {
    setFormData(prev => ({ ...prev, scopeNodeIds: prev.scopeNodeIds.filter(i => i !== nodeId) }));
    setScopeNodeNames(prev => { const next = { ...prev }; delete next[nodeId]; return next; });
  };

  const cleanWorkflowForApi = (workflow) => {
    if (!workflow?.steps) return { name: "", steps: [] };
    const stripId = (obj) => { const copy = { ...obj }; delete copy.id; return copy; };
    return {
      name: "",
      steps: workflow.steps.map(rawStep => {
        const wfStep = stripId(rawStep);
        return {
          ...wfStep,
          stepType: typeof wfStep.stepType === "string"
            ? (WORKFLOW_STEP_TYPE[wfStep.stepType] || WORKFLOW_STEP_TYPE.Form)
            : wfStep.stepType,
          fields: (wfStep.fields || []).map(rawField => {
            const field = stripId(rawField);
            return {
              ...field,
              fieldType: typeof field.type === "string"
                ? (STEP_FIELD_TYPE[field.type] || STEP_FIELD_TYPE.Text)
                : field.fieldType,
              isRequired: field.required || false,
              options: field.options || []
            };
          })
        };
      })
    };
  };

  const validateStep = (currentStep = step) => {
    if (currentStep === 1 && !formData.name.trim()) { setError(t("sb_name_required")); return false; }
    if (currentStep === 2 && formData.scopeNodeIds.length === 0) { setError(t("sb_scope_required")); return false; }
    if (currentStep === 2 && formData.isPaid && formData.price <= 0) { setError(t("sb_price_positive")); return false; }
    if (currentStep === 3 && (!formData.workflow?.steps?.length)) { setError(t("sb_workflow_required")); return false; }
    return true;
  };

  const handleNext = () => {
    if (validateStep()) { setError(null); setStep(s => Math.min(3, s + 1)); }
  };

  const handleSave = async () => {
    for (let s = 1; s <= 3; s++) {
      if (!validateStep(s)) { setStep(s); return; }
    }
    setSaving(true); setError(null);
    try {
      const payload = {
        name: formData.name,
        type: typeof formData.type === "string" ? getTypeNumber(formData.type) : formData.type,
        description: formData.description,
        isPaid: formData.isPaid,
        price: formData.price,
        scopeNodeIds: formData.scopeNodeIds,
        includeDescendants: formData.includeDescendants,
        academicYearId: formData.academicYearId || null,
        semesterId: formData.semesterId || null,
        levelOrder: formData.levelOrder,
        workflow: cleanWorkflowForApi(formData.workflow)
      };
      if (id) { await editService(id, payload); addToast(t("service_updated"), "success"); }
      else { await addService(payload); addToast(t("service_created"), "success"); }
      navigate("/admin/student-services/services");
    } catch (e) {
      const msg = e.response?.data?.title || e.response?.data?.message || e.message || t("something_went_wrong");
      setError(msg);
      addToast(msg, "error");
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <LoadingSpinner />;

  return (
    <div className="service-builder-container">
      <PageHeader
        icon={Settings}
        title={id ? t("edit_service") : t("create_service")}
        subtitle={id ? t("edit_service_desc") : t("create_service_desc")}
        leading={
          <button
            className="btn-icon"
            onClick={() => navigate("/admin/student-services/services")}
            title={t("back_to_list")}
          >
            <ArrowLeft size={16} />
          </button>
        }
      />

      {error && <div className="sb-error">{error}</div>}

      {/* Step progress indicator */}
      <div className="sb-stepper">
        {STEPS.map((s, idx) => (
          <div
            key={s.key}
            className={`sb-step ${step === idx + 1 ? "active" : step > idx + 1 ? "done" : ""}`}
            onClick={() => { if (idx + 1 <= step) setStep(idx + 1); }}
          >
            <div className="sb-step-number">
              {step > idx + 1 ? <CheckCircle size={13} /> : idx + 1}
            </div>
            <span>{t(s.key)}</span>
            {idx < 2 && <ChevronRight size={13} className="sb-step-arrow" />}
          </div>
        ))}
      </div>

      {/* Step content */}
      <div className="sb-step-content">
        {step === 1 && (
          <BasicInfo t={t} formData={formData} updateField={updateField} serviceTypes={serviceTypes} />
        )}
        {step === 2 && (
          <EligibilityPricing
            t={t} formData={formData} updateField={updateField}
            academicYears={academicYears} semesters={semesters}
            handleYearChange={handleYearChange} removeScopeNode={removeScopeNode}
            scopeNodeNames={scopeNodeNames} setShowScopeTree={setShowScopeTree}
          />
        )}
        {step === 3 && (
          <WorkflowStep t={t} formData={formData} updateField={updateField} />
        )}
      </div>

      {/* Navigation actions */}
      <div className="sb-actions">
        <button
          className="btn-secondary"
          onClick={() => setStep(s => Math.max(1, s - 1))}
          disabled={step === 1}
        >
          {t("previous")}
        </button>
        {step < 3 ? (
          <button className="btn-primary" onClick={handleNext}>
            {t("next")} <ChevronRight size={14} />
          </button>
        ) : (
          <button className="btn-primary" onClick={handleSave} disabled={saving}>
            {saving ? t("saving") : t("submit")}
          </button>
        )}
      </div>

      <ScopeTreeModal
        isOpen={showScopeTree}
        onClose={() => setShowScopeTree(false)}
        onSelect={handleNodeSelect}
      />
    </div>
  );
};

export default ServiceBuilder;
