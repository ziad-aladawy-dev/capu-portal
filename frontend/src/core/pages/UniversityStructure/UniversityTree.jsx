import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ChevronRight, ChevronDown, Building2, BookOpen, GraduationCap,
  Plus, Trash2, RefreshCw, X, University, CheckCircle2, Globe
} from 'lucide-react';
import universityService from '../../api/universityService';
import authService from '../../api/authService';
import Sidebar from '../../layouts/Sidebar/Sidebar';
import TopNav from '../../layouts/TopNav/TopNav';
import LoadingSpinner from '../../components/UI/LoadingSpinner';
import ErrorMessage from '../../components/UI/ErrorMessage';
import './UniversityTree.css';

const UniversityTree = () => {
  const navigate = useNavigate();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [universities, setUniversities] = useState([]);
  const [selectedUniversityId, setSelectedUniversityId] = useState(null);
  const [treeData, setTreeData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadingTree, setLoadingTree] = useState(false);
  const [error, setError] = useState(null);
  const [expandedNodes, setExpandedNodes] = useState({});
  const [showAddForm, setShowAddForm] = useState(false);
  const [addType, setAddType] = useState(null);
  const [parentId, setParentId] = useState(null);
  const [parentName, setParentName] = useState('');
  const [systemTypes, setSystemTypes] = useState([]);
  const [submitting, setSubmitting] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);

  const currentUser = authService.getCurrentUser();

  const [formData, setFormData] = useState({
    code: '',
    nameAr: '',
    nameEn: '',
    domain: '',
    logoUrl: '',
    systemTypeId: '',
    orderNumber: 1
  });

  useEffect(() => {
    loadUniversities();
    loadSystemTypes();
  }, []);

  useEffect(() => {
    if (selectedUniversityId) {
      loadTree(selectedUniversityId);
    }
  }, [selectedUniversityId]);

  const loadUniversities = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await universityService.getAllUniversities();
      setUniversities(data);
      if (data.length > 0 && !selectedUniversityId) {
        setSelectedUniversityId(data[0].id);
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const loadTree = async (universityId) => {
    setLoadingTree(true);
    try {
      const data = await universityService.getUniversityTree(universityId);
      setTreeData(data);
      setExpandedNodes({ [data.id]: true });
    } catch (err) {
      console.error('Failed to load tree:', err);
    } finally {
      setLoadingTree(false);
    }
  };

  const loadSystemTypes = async () => {
    try {
      const types = await universityService.getSystemTypes();
      setSystemTypes(types);
    } catch (err) {
      console.error('Failed to load system types:', err);
    }
  };

  const toggleExpand = (nodeId) => {
    setExpandedNodes(prev => ({
      ...prev,
      [nodeId]: !prev[nodeId]
    }));
  };

  const handleAddClick = (type, parentId = null, parentName = '') => {
    setAddType(type);
    setParentId(parentId);
    setParentName(parentName);
    setFormData({
      code: '',
      nameAr: '',
      nameEn: '',
      domain: '',
      logoUrl: '',
      systemTypeId: '',
      orderNumber: 1
    });
    setShowAddForm(true);
  };

  const handleFormChange = (e) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
  };

  const handleAddSubmit = async (e) => {
    e.preventDefault();
    setSubmitting(true);

    try {
      let result;
      if (addType === 'university') {
        result = await universityService.addUniversity({
          nameAr: formData.nameAr,
          nameEn: formData.nameEn,
          domain: formData.domain,
          logoUrl: formData.logoUrl
        });
        if (result) {
          await loadUniversities();
          if (result.id) setSelectedUniversityId(result.id);
        }
      } else if (addType === 'faculty') {
        result = await universityService.addFaculty({
          universityId: parentId || selectedUniversityId,
          code: formData.code,
          nameAr: formData.nameAr,
          nameEn: formData.nameEn
        });
        if (result) await loadTree(selectedUniversityId);
      } else if (addType === 'program') {
        result = await universityService.addProgram({
          facultyId: parentId,
          code: formData.code,
          nameAr: formData.nameAr,
          nameEn: formData.nameEn,
          systemTypeId: formData.systemTypeId
        });
        if (result) await loadTree(selectedUniversityId);
      } else if (addType === 'level') {
        result = await universityService.addLevel({
          programId: parentId,
          code: formData.code,
          nameAr: formData.nameAr,
          nameEn: formData.nameEn,
          orderNumber: parseInt(formData.orderNumber)
        });
        if (result) await loadTree(selectedUniversityId);
      }

      if (result) {
        setShowSuccess(true);
        setTimeout(() => {
          setShowSuccess(false);
          setShowAddForm(false);
        }, 1500);
      }
    } catch (err) {
      alert(err.message);
    } finally {
      setSubmitting(false);
    }
  };

  const handleDelete = async (type, id, name) => {
    if (!window.confirm(`Are you sure you want to delete "${name}"?`)) return;

    try {
      if (type === 'faculty') {
        await universityService.deleteFaculty(id);
      } else if (type === 'program') {
        await universityService.deleteProgram(id);
      } else if (type === 'level') {
        await universityService.deleteLevel(id);
      }
      await loadTree(selectedUniversityId);
    } catch (err) {
      alert(err.message);
    }
  };

  const renderTreeNode = (node, type, level = 0) => {
    const isExpanded = expandedNodes[node.id];
    const hasChildren = (type === 'university' && node.faculties?.length > 0) ||
                        (type === 'faculty' && node.programs?.length > 0) ||
                        (type === 'program' && node.levels?.length > 0);

    const getIcon = () => {
      if (type === 'university') return <University size={18} />;
      if (type === 'faculty') return <Building2 size={18} />;
      if (type === 'program') return <BookOpen size={18} />;
      return <GraduationCap size={18} />;
    };

    const getAddButtonType = () => {
      if (type === 'university') return 'faculty';
      if (type === 'faculty') return 'program';
      if (type === 'program') return 'level';
      return null;
    };

    return (
      <li key={node.id} className="tree-node-item">
        <div className={`tree-node-content ${isExpanded ? 'expanded' : ''}`}>
          <div className="tree-expand-icon" onClick={() => hasChildren && toggleExpand(node.id)}>
            {hasChildren && (isExpanded ? <ChevronDown size={18} /> : <ChevronRight size={18} />)}
          </div>
          <div className="tree-node-icon">
            {getIcon()}
          </div>
          <div className="tree-node-info">
            <div className="tree-node-name">
              {node.nameEn}
              {node.code && <span style={{ fontSize: '12px', color: 'var(--text-muted)', marginLeft: '8px' }}>({node.code})</span>}
            </div>
            <div className="tree-node-code">
              {type === 'program' && node.systemType && `${node.systemType.nameEn} · `}
              {type === 'level' && `Level ${node.orderNumber} · `}
              {node.nameAr && ` ${node.nameAr}`}
              {type === 'university' && node.domain && ` · ${node.domain}`}
            </div>
          </div>
          <div className="tree-node-actions">
            {getAddButtonType() && (
              <button
                className="tree-action-btn"
                onClick={() => handleAddClick(getAddButtonType(), node.id, node.nameEn)}
                title={`Add ${getAddButtonType()}`}
              >
                <Plus size={16} />
              </button>
            )}
            {type !== 'university' && (
              <button
                className="tree-action-btn delete"
                onClick={() => handleDelete(type, node.id, node.nameEn)}
                title="Delete"
              >
                <Trash2 size={16} />
              </button>
            )}
          </div>
        </div>
        
        {hasChildren && isExpanded && (
          <ul className="tree-children">
            {type === 'university' && node.faculties.map(f => renderTreeNode(f, 'faculty', level + 1))}
            {type === 'faculty' && node.programs.map(p => renderTreeNode(p, 'program', level + 1))}
            {type === 'program' && node.levels.map(l => renderTreeNode(l, 'level', level + 1))}
          </ul>
        )}
      </li>
    );
  };

  const getAddFormTitle = () => {
    if (addType === 'university') return 'Add New University';
    if (addType === 'faculty') return 'Add New Faculty';
    if (addType === 'program') return `Add New Program to "${parentName}"`;
    if (addType === 'level') return `Add New Level to "${parentName}"`;
    return '';
  };

  if (loading) return <LoadingSpinner fullPage message="Loading universities..." />;
  if (error) return <ErrorMessage message={error} onRetry={loadUniversities} />;

  return (
    <div className="dashboard-container">
      <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />
      <TopNav onMenuClick={() => setSidebarOpen(true)} />

      <div className="main-content">
        <div className="university-tree-container">
          <div className="tree-header">
            <div className="tree-title">
              <Building2 size={28} color="var(--gold)" />
              <h2>University Structure</h2>
            </div>
            <div className="tree-actions">
              <button className="refresh-btn" onClick={() => handleAddClick('university')}>
                <Plus size={16} />
                Add University
              </button>
            </div>
          </div>

          {/* University Selector */}
          {universities.length > 1 && (
            <div className="university-selector">
              <label>Select University:</label>
              <select
                value={selectedUniversityId || ''}
                onChange={(e) => setSelectedUniversityId(e.target.value)}
              >
                {universities.map(uni => (
                  <option key={uni.id} value={uni.id}>
                    {uni.nameEn} - {uni.nameAr}
                  </option>
                ))}
              </select>
            </div>
          )}

          {/* Tree Structure */}
          <div className="tree-structure">
            {loadingTree ? (
              <div style={{ textAlign: 'center', padding: '40px' }}>
                <LoadingSpinner message="Loading structure..." />
              </div>
            ) : treeData ? (
              <ul className="tree-node">
                {renderTreeNode(treeData, 'university')}
              </ul>
            ) : (
              <div className="empty-tree">
                <University size={48} />
                <p>No university structure data available</p>
                <button
                  className="refresh-btn"
                  onClick={() => handleAddClick('university')}
                  style={{ marginTop: '16px' }}
                >
                  <Plus size={16} />
                  Add Your First University
                </button>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Add Form Modal */}
      {showAddForm && (
        <div className="add-form-overlay" onClick={() => setShowAddForm(false)}>
          <div className="add-form-container" onClick={(e) => e.stopPropagation()}>
            <div className="add-form-header">
              <h3>{getAddFormTitle()}</h3>
              <button className="close-form" onClick={() => setShowAddForm(false)}>
                <X size={20} />
              </button>
            </div>
            <form onSubmit={handleAddSubmit}>
              <div className="add-form-body">
                {addType === 'university' ? (
                  <>
                    <div className="form-group">
                      <label>University Name (English) *</label>
                      <input
                        type="text"
                        name="nameEn"
                        value={formData.nameEn}
                        onChange={handleFormChange}
                        placeholder="e.g., Cairo University"
                        required
                      />
                    </div>
                    <div className="form-group">
                      <label>University Name (Arabic) *</label>
                      <input
                        type="text"
                        name="nameAr"
                        value={formData.nameAr}
                        onChange={handleFormChange}
                        placeholder="e.g., جامعة القاهرة"
                        required
                      />
                    </div>
                    <div className="form-group">
                      <label>Domain *</label>
                      <input
                        type="text"
                        name="domain"
                        value={formData.domain}
                        onChange={handleFormChange}
                        placeholder="e.g., cu.edu.eg"
                        required
                      />
                    </div>
                    <div className="form-group">
                      <label>Logo URL</label>
                      <input
                        type="text"
                        name="logoUrl"
                        value={formData.logoUrl}
                        onChange={handleFormChange}
                        placeholder="https://..."
                      />
                    </div>
                  </>
                ) : (
                  <>
                    <div className="form-group">
                      <label>Code *</label>
                      <input
                        type="text"
                        name="code"
                        value={formData.code}
                        onChange={handleFormChange}
                        placeholder={addType === 'faculty' ? "e.g., ENG" : addType === 'program' ? "e.g., CS" : "e.g., L1"}
                        required
                      />
                    </div>
                    <div className="form-group">
                      <label>Name (English) *</label>
                      <input
                        type="text"
                        name="nameEn"
                        value={formData.nameEn}
                        onChange={handleFormChange}
                        required
                      />
                    </div>
                    <div className="form-group">
                      <label>Name (Arabic) *</label>
                      <input
                        type="text"
                        name="nameAr"
                        value={formData.nameAr}
                        onChange={handleFormChange}
                        required
                      />
                    </div>
                    {addType === 'program' && (
                      <div className="form-group">
                        <label>System Type *</label>
                        <select
                          name="systemTypeId"
                          value={formData.systemTypeId}
                          onChange={handleFormChange}
                          required
                        >
                          <option value="">Select System Type</option>
                          {systemTypes.map(type => (
                            <option key={type.id} value={type.id}>
                              {type.nameEn} - {type.nameAr}
                            </option>
                          ))}
                        </select>
                      </div>
                    )}
                    {addType === 'level' && (
                      <div className="form-group">
                        <label>Order Number *</label>
                        <input
                          type="number"
                          name="orderNumber"
                          value={formData.orderNumber}
                          onChange={handleFormChange}
                          min="1"
                          max="10"
                          required
                        />
                      </div>
                    )}
                  </>
                )}
                <div className="form-actions">
                  <button type="button" className="cancel-btn" onClick={() => setShowAddForm(false)}>
                    Cancel
                  </button>
                  <button type="submit" className="submit-btn" disabled={submitting}>
                    {submitting ? 'Adding...' : 'Add'}
                  </button>
                </div>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Success Message */}
      {showSuccess && (
        <div className="add-form-overlay" style={{ zIndex: 2000 }}>
          <div className="add-form-container" style={{ textAlign: 'center', maxWidth: '400px' }}>
            <div style={{ padding: '40px' }}>
              <div style={{
                width: '70px',
                height: '70px',
                background: 'rgba(22,163,74,0.1)',
                borderRadius: '50%',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                margin: '0 auto 20px'
              }}>
                <CheckCircle2 size={40} color="#16a34a" />
              </div>
              <h3 style={{ color: 'var(--navy-primary)', marginBottom: '8px' }}>Added Successfully!</h3>
              <p style={{ color: 'var(--text-muted)' }}>The item has been added to the structure.</p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default UniversityTree;