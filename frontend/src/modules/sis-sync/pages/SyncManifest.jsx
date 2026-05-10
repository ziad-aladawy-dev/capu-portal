import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Database, Upload, RefreshCw, CheckCircle2, XCircle,
  Clock, Users, UserCheck, BookOpen, FileText, ChevronDown, ChevronRight
} from 'lucide-react';
import syncService from '../api/syncService';
import authService from '../../../core/api/authService';
import Sidebar from '../../../core/layouts/Sidebar/Sidebar';
import TopNav from '../../../core/layouts/TopNav/TopNav';

import './SyncManifest.css';

const SyncManifest = () => {
  const navigate = useNavigate();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [activeTab, setActiveTab] = useState('full');
  const [jsonInput, setJsonInput] = useState('');
  const [jsonError, setJsonError] = useState('');
  const [syncing, setSyncing] = useState(false);
  const [syncResults, setSyncResults] = useState(null);
  const [syncStatus, setSyncStatus] = useState(null);
  const [loadingStatus, setLoadingStatus] = useState(false);
  const [showSample, setShowSample] = useState(false);

  const currentUser = authService.getCurrentUser();

  // Sample manifest template
  const sampleManifest = {
    sourceSystem: "SIS",
    version: "1.0",
    timestamp: new Date().toISOString(),
    correlationId: "sync-" + new Date().getTime(),
    students: [
      {
        nationalId: "29801230123456",
        studentCode: "STU20240001",
        fullNameAr: "أحمد محمد علي",
        fullNameEn: "Ahmed Mohamed Ali",
        email: "ahmed.ali@student.cu.edu.eg",
        phone: "01001234567",
        dateOfBirth: "2000-01-15T00:00:00",
        programCode: "CS",
        levelCode: "L1",
        enrollmentDate: "2024-09-01T00:00:00"
      }
    ],
    staff: [
      {
        staffCode: "PROF002",
        fullNameAr: "أستاذ خالد محمد",
        fullNameEn: "Prof. Khaled Mohamed",
        email: "khaled.mohamed@capital.edu.eg",
        phone: "01009876543",
        position: "Professor",
        department: "Computer Science"
      }
    ],
    courses: [
      {
        courseCode: "CS201",
        nameAr: "هياكل البيانات",
        nameEn: "Data Structures",
        creditHours: 3,
        levelId: "00000000-0000-0000-0000-000000000000"
      }
    ]
  };



  const loadSyncStatus = async () => {
    // setLoadingStatus(true);
    try {
      const status = await syncService.getSyncStatus();
      setSyncStatus(status);
    } catch (err) {
      console.error('Failed to load sync status:', err);
    } finally {
      // setLoadingStatus(false);
    }
  };

  useEffect(() => {
    loadSyncStatus();
  }, []);



  const handleJsonChange = (e) => {
    const value = e.target.value;
    setJsonInput(value);
    setJsonError('');
    setSyncResults(null);
  };

  const loadSample = () => {
    setJsonInput(JSON.stringify(sampleManifest, null, 2));
    setJsonError('');
    setSyncResults(null);
  };

  const validateAndParseJson = () => {
    if (!jsonInput.trim()) {
      setJsonError('Please enter manifest JSON data');
      return null;
    }

    try {
      const parsed = JSON.parse(jsonInput);
      
      // Basic validation
      if (!parsed.sourceSystem) {
        setJsonError('Manifest must contain "sourceSystem" field');
        return null;
      }
      if (!parsed.timestamp) {
        setJsonError('Manifest must contain "timestamp" field');
        return null;
      }
      
      return parsed;
    } catch (err) {
      setJsonError(`Invalid JSON: ${err.message}`);
      return null;
    }
  };

  const handleSync = async () => {
    const manifest = validateAndParseJson();
    if (!manifest) return;

    setSyncing(true);
    setSyncResults(null);
    setJsonError('');

    try {
      let result;
      if (activeTab === 'students') {
        result = await syncService.syncStudents(manifest);
      } else if (activeTab === 'staff') {
        result = await syncService.syncStaff(manifest);
      } else if (activeTab === 'courses') {
        result = await syncService.syncCourses(manifest);
      } else {
        result = await syncService.syncManifest(manifest);
      }

      setSyncResults({
        ...result,
        correlationId: manifest.correlationId,
        sourceSystem: manifest.sourceSystem,
        timestamp: manifest.timestamp
      });
      
      loadSyncStatus();
    } catch (err) {
      setJsonError(err.message);
    } finally {
      setSyncing(false);
    }
  };

  const getTabTitle = () => {
    switch (activeTab) {
      case 'students': return 'Sync Students Only';
      case 'staff': return 'Sync Staff Only';
      case 'courses': return 'Sync Courses Only';
      default: return 'Full Manifest Sync';
    }
  };

  const getTabDescription = () => {
    switch (activeTab) {
      case 'students':
        return 'Upload a manifest containing student records to sync with the system';
      case 'staff':
        return 'Upload a manifest containing staff records to sync with the system';
      case 'courses':
        return 'Upload a manifest containing course records to sync with the system';
      default:
        return 'Upload a complete manifest containing students, staff, and courses to sync with the system';
    }
  };

  return (
    <div className="dashboard-container">
      <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />
      <TopNav onMenuClick={() => setSidebarOpen(true)} />

      <div className="main-content">
        <div className="sync-container">
          <div className="sync-header">
            <div className="sync-title">
              <Database size={28} color="var(--gold)" />
              <h2>Integration Sync</h2>
            </div>
            <div className="sync-status">
              <button className="refresh-btn" onClick={loadSyncStatus} style={{ padding: '8px 16px' }}>
                <RefreshCw size={16} />
                Check Status
              </button>
              {syncStatus && (
                <div className={`status-indicator ${syncStatus.isHealthy ? 'healthy' : 'unhealthy'}`}>
                  <span className="status-dot"></span>
                  {syncStatus.isHealthy ? 'System Healthy' : 'Issues Detected'}
                </div>
              )}
            </div>
          </div>

          <div className="sync-tabs">
            <button
              className={`sync-tab ${activeTab === 'full' ? 'active' : ''}`}
              onClick={() => setActiveTab('full')}
            >
              Full Manifest
            </button>
            <button
              className={`sync-tab ${activeTab === 'students' ? 'active' : ''}`}
              onClick={() => setActiveTab('students')}
            >
              Students Only
            </button>
            <button
              className={`sync-tab ${activeTab === 'staff' ? 'active' : ''}`}
              onClick={() => setActiveTab('staff')}
            >
              Staff Only
            </button>
            <button
              className={`sync-tab ${activeTab === 'courses' ? 'active' : ''}`}
              onClick={() => setActiveTab('courses')}
            >
              Courses Only
            </button>
          </div>

          <div className="manifest-form">
            <div style={{ marginBottom: '16px' }}>
              <h3 style={{ fontSize: '16px', marginBottom: '8px' }}>{getTabTitle()}</h3>
              <p style={{ color: 'var(--text-muted)', fontSize: '13px' }}>{getTabDescription()}</p>
            </div>

            <div className="json-editor">
              <textarea
                value={jsonInput}
                onChange={handleJsonChange}
                placeholder={`Paste your manifest JSON here...\n\nExample:\n${JSON.stringify(sampleManifest, null, 2)}`}
              />
            </div>

            {jsonError && (
              <div className="json-error">
                <XCircle size={16} style={{ marginRight: '8px', verticalAlign: 'middle' }} />
                {jsonError}
              </div>
            )}

            <div className="sync-actions">
              <button className="sync-btn secondary" onClick={loadSample}>
                <FileText size={16} />
                Load Sample
              </button>
              <button className="sync-btn primary" onClick={handleSync} disabled={syncing}>
                <Upload size={16} />
                {syncing ? 'Syncing...' : 'Sync Now'}
              </button>
            </div>

            {/* Sample JSON dropdown */}
            <div className="sample-json">
              <summary onClick={() => setShowSample(!showSample)} style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '8px' }}>
                {showSample ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
                View Sample Manifest Structure
              </summary>
              {showSample && (
                <pre>{JSON.stringify(sampleManifest, null, 2)}</pre>
              )}
            </div>
          </div>

          {/* Sync Results */}
          {syncResults && (
            <div className="sync-results">
              <div className="results-card">
                <div className="results-header">
                  <CheckCircle2 size={20} color="#16a34a" />
                  <h3>Sync Completed Successfully</h3>
                </div>
                <div className="results-grid">
                  {syncResults.results && syncResults.results.map((result, idx) => (
                    <div key={idx} className="result-item">
                      <span className="result-label">{result.entityType}</span>
                      <span className="result-value">{result.syncedCount} synced</span>
                    </div>
                  ))}
                  {syncResults.StudentsSynced !== undefined && (
                    <>
                      <div className="result-item">
                        <span className="result-label">Students Synced</span>
                        <span className="result-value">{syncResults.StudentsSynced || 0}</span>
                      </div>
                      <div className="result-item">
                        <span className="result-label">Staff Synced</span>
                        <span className="result-value">{syncResults.StaffSynced || 0}</span>
                      </div>
                      <div className="result-item">
                        <span className="result-label">Courses Synced</span>
                        <span className="result-value">{syncResults.CoursesSynced || 0}</span>
                      </div>
                    </>
                  )}
                </div>
                <div className="correlation-id">
                  Correlation ID: {syncResults.correlationId}
                </div>
                <div className="correlation-id">
                  Source: {syncResults.sourceSystem} · {new Date(syncResults.timestamp).toLocaleString()}
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default SyncManifest;