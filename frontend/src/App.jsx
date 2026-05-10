import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import './App.css';

// Pages
import LandingPage from './core/pages/Landing/LandingPage';
import Login from './core/auth/pages/Login';
import AdminDashboard from './core/pages/Dashboard/AdminDashboard';
import UniversityTree from './core/pages/UniversityStructure/UniversityTree';
import PermissionManagement from './core/pages/Permissions/PermissionManagement';
import SyncManifest from './modules/sis-sync/pages/SyncManifest';

import UserManagement from './core/pages/Users/UserManagement';
import AddStudent from './core/pages/Users/AddStudent';
import AddStaff from './core/pages/Users/AddStaff';
import EditStudent from './core/pages/Users/EditStudent';
import EditStaff from './core/pages/Users/EditStaff';
import UserDetails from './core/pages/Users/UserDetails';

// Components
import ProtectedRoute from './core/guards/ProtectedRoute';

// Styles
import './styles/global.css';

function App() {
  return (
    <div className="App">
      <Routes>
        {/* Public Routes */}
        <Route path="/" element={<LandingPage />} />
        <Route path="/login" element={<Login />} />

        {/* Protected Routes */}
        <Route
          path="/dashboard"
          element={
            <ProtectedRoute>
              <AdminDashboard />
            </ProtectedRoute>
          }
        />
        <Route
          path="/university-tree"
          element={
            <ProtectedRoute>
              <UniversityTree />
            </ProtectedRoute>
          }
        />
        <Route
          path="/permissions"
          element={
            <ProtectedRoute>
              <PermissionManagement />
            </ProtectedRoute>
          }
        />
        <Route
          path="/sync"
          element={
            <ProtectedRoute>
              <SyncManifest />
            </ProtectedRoute>
          }
        />
        <Route
  path="/users"
  element={
    <ProtectedRoute>
      <UserManagement />
    </ProtectedRoute>
  }
/>
<Route
  path="/users/add-student"
  element={
    <ProtectedRoute>
      <AddStudent />
    </ProtectedRoute>
  }
/>
<Route
  path="/users/add-staff"
  element={
    <ProtectedRoute>
      <AddStaff />
    </ProtectedRoute>
  }
/>
<Route
  path="/users/edit-student/:id"
  element={
    <ProtectedRoute>
      <EditStudent />
    </ProtectedRoute>
  }
/>
<Route
  path="/users/edit-staff/:id"
  element={
    <ProtectedRoute>
      <EditStaff />
    </ProtectedRoute>
  }
/>
<Route
  path="/users/:id"
  element={
    <ProtectedRoute>
      <UserDetails />
    </ProtectedRoute>
  }
/>
        <Route
          path="/faculties"
          element={
            <ProtectedRoute>
              <div className="main-content" style={{ textAlign: 'center', padding: '60px' }}>
                <h2>Faculty Management</h2>
                <p>Coming soon...</p>
              </div>
            </ProtectedRoute>
          }
        />
        <Route
          path="/courses"
          element={
            <ProtectedRoute>
              <div className="main-content" style={{ textAlign: 'center', padding: '60px' }}>
                <h2>Course Management</h2>
                <p>Coming soon...</p>
              </div>
            </ProtectedRoute>
          }
        />
        <Route
          path="/reports"
          element={
            <ProtectedRoute>
              <div className="main-content" style={{ textAlign: 'center', padding: '60px' }}>
                <h2>Reports</h2>
                <p>Coming soon...</p>
              </div>
            </ProtectedRoute>
          }
        />
        <Route
          path="/settings"
          element={
            <ProtectedRoute>
              <div className="main-content" style={{ textAlign: 'center', padding: '60px' }}>
                <h2>Settings</h2>
                <p>Coming soon...</p>
              </div>
            </ProtectedRoute>
          }
        />

        {/* Fallback */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </div>
  );
}

export default App;
