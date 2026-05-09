import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import './App.css';

// Pages
import LandingPage from './pages/Landing/LandingPage';
import Login from './pages/Auth/Login';
import AdminDashboard from './pages/Dashboard/AdminDashboard';
import UniversityTree from './pages/UniversityStructure/UniversityTree';
import PermissionManagement from './pages/Permissions/PermissionManagement';
import SyncManifest from './pages/Integration/SyncManifest';

import UserManagement from './pages/Users/UserManagement';
import AddStudent from './pages/Users/AddStudent';
import AddStaff from './pages/Users/AddStaff';
import EditStudent from './pages/Users/EditStudent';
import EditStaff from './pages/Users/EditStaff';
import UserDetails from './pages/Users/UserDetails';

// Components
import ProtectedRoute from './components/Routing/ProtectedRoute';

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