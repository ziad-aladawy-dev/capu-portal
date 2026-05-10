import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Bell, Users, Building2, BookOpen, GraduationCap,
  UserCircle2, Activity, Home, BarChart3,
  Search, Moon, ChevronRight
} from 'lucide-react';
import authService from '../../api/authService';
import Sidebar from '../../layouts/Sidebar/Sidebar';
import TopNav from '../../layouts/TopNav/TopNav';

const AdminDashboard = () => {
  const navigate = useNavigate();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [windowWidth, setWindowWidth] = useState(
    typeof window !== 'undefined' ? window.innerWidth : 1200
  );

  useEffect(() => {
    const handleResize = () => setWindowWidth(window.innerWidth);
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, []);

  const isMobile = windowWidth < 640;
  const isTablet = windowWidth >= 640 && windowWidth < 1024;

  const stats = [
    { label: "Total Students",  value: "8,945", icon: GraduationCap, trend: "+12% from last month", trendColor: "#16a34a", iconClass: "students" },
    { label: "Faculties", value: "12",     icon: Building2,     trend: "Stable",               trendColor: "#c9a84c", iconClass: "faculties" },
    { label: "Active Courses",  value: "342",    icon: BookOpen,      trend: "+5% from last month",  trendColor: "#16a34a", iconClass: "courses" },
    { label: "Faculty Members",     value: "456",    icon: UserCircle2,   trend: "+2 this month",    trendColor: "#16a34a", iconClass: "instructors" },
  ];

  const recentActivities = [
    { id: 1, action: "New student registration",    user: "Ahmed Hassan",     time: "5 minutes ago",  dot: "#16a34a" },
    { id: 2, action: "Course 'AI' updated",       user: "Dr. Ali Ibrahim",  time: "12 minutes ago", dot: "#c9a84c" },
    { id: 3, action: "New faculty added",         user: "Faculty of Pharmacy", time: "1 hour ago",  dot: "#2e3591" },
    { id: 4, action: "Instructor profile updated", user: "Dr. Sara Nour",    time: "2 hours ago", dot: "#c9a84c" },
    { id: 5, action: "Course 'Networks' created", user: "Dr. Khaled Omar",  time: "3 hours ago", dot: "#16a34a" },
  ];

  const quickActions = [
    { label: "Manage Departments", action: () => navigate('/departments') },
    { label: "Add New User", action: () => navigate('/users/add') },
    { label: "Add New Faculty", action: () => navigate('/faculties/add') },
    { label: "Add New Course", action: () => navigate('/courses/add') },
    { label: "View Reports", action: () => navigate('/reports') },
  ];

  const iconColors = {
    students:    { background: 'rgba(26,31,94,0.08)',    color: '#1a1f5e' },
    faculties:   { background: 'rgba(201,168,76,0.12)',  color: '#7a5c10' },
    courses:     { background: 'rgba(96,165,250,0.12)',  color: '#2563eb' },
    instructors: { background: 'rgba(244,114,182,0.12)', color: '#be185d' },
  };

  return (
    <>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Space+Mono:wght@400;700&family=DM+Sans:wght@400;500;600;700&display=swap');
        *, *::before, *::after { margin: 0; padding: 0; box-sizing: border-box; }
        html, body { font-family: 'DM Sans', sans-serif; overflow-x: hidden; width: 100%; }

        /* ── Animations ── */
        @keyframes pulse     { 0%,100%{opacity:1} 50%{opacity:0.4} }
        @keyframes fadeDown  { from{opacity:0;transform:translateY(-24px)} to{opacity:1;transform:translateY(0)} }
        @keyframes fadeUp    { from{opacity:0;transform:translateY(24px)}  to{opacity:1;transform:translateY(0)} }
        @keyframes fadeIn    { from{opacity:0;transform:translateY(12px)}  to{opacity:1;transform:translateY(0)} }
        @keyframes scaleIn   { from{opacity:0;transform:scale(0.96)}       to{opacity:1;transform:scale(1)} }

        .anim-header   { animation: fadeDown 0.55s ease both; }
        .anim-search   { animation: fadeDown 0.55s ease 0.08s both; }
        .anim-s0       { animation: fadeIn  0.5s ease 0.12s both; }
        .anim-s1       { animation: fadeIn  0.5s ease 0.20s both; }
        .anim-s2       { animation: fadeIn  0.5s ease 0.28s both; }
        .anim-s3       { animation: fadeIn  0.5s ease 0.36s both; }
        .anim-activities { animation: fadeUp 0.55s ease 0.42s both; }
        .anim-actions    { animation: fadeUp 0.55s ease 0.52s both; }

        /* ── Hover effects ── */
        .stat-card { transition: transform 0.3s ease, box-shadow 0.3s ease; }
        .stat-card:hover { transform: translateY(-5px) !important; box-shadow: 0 12px 32px rgba(26,31,94,0.13) !important; }

        .qbtn { transition: all 0.25s; }
        .qbtn:hover { background: #fdf6e3 !important; border-color: rgba(201,168,76,0.4) !important; transform: translateX(5px); }

        .nav-btn { transition: all 0.2s; }
        .nav-btn:hover { background: rgba(255,255,255,0.1) !important; }

        .act-item { transition: all 0.2s; }
        .act-item:hover { background: #fdf6e3 !important; border-color: rgba(201,168,76,0.25) !important; }

        .close-x:hover  { background: rgba(220,38,38,0.2) !important; color: #fca5a5 !important; }
        .logout-btn:hover { background: rgba(220,38,38,0.1) !important; }

        .search-input { transition: border-color 0.25s, box-shadow 0.25s, background 0.25s; }
        .search-input:focus {
          outline: none;
          border-color: #c9a84c !important;
          background: #fff !important;
          box-shadow: 0 0 0 4px rgba(201,168,76,0.12) !important;
        }

        .a-dot {
          display: inline-block;
          border-radius: 50%;
          flex-shrink: 0;
          margin-top: 5px;
          animation: pulse 2.2s ease-in-out infinite;
        }

        /* ── Responsive grids ── */
        .stats-grid {
          display: grid;
          width: 100%;
          margin-bottom: 20px;
          gap: 14px;
          /* mobile default: single column */
          grid-template-columns: 1fr;
        }
        @media (min-width: 640px)  { .stats-grid { grid-template-columns: 1fr 1fr; gap: 16px; } }
        @media (min-width: 1024px) { .stats-grid { grid-template-columns: repeat(4,1fr); gap: 20px; margin-bottom: 24px; } }

        .content-grid {
          display: grid;
          width: 100%;
          gap: 16px;
          grid-template-columns: 1fr;
        }
        @media (min-width: 1024px) { .content-grid { grid-template-columns: 1.5fr 1fr; gap: 24px; } }

        /* ── Hide welcome on mobile ── */
        @media (max-width: 639px) { .welcome-block { display: none !important; } }
      `}</style>

      {/* Use the Sidebar component */}
      <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />

      {/* Use the TopNav component */}
      <TopNav onMenuClick={() => setSidebarOpen(true)} />

      {/* ── Page ── */}
      <div style={{ minHeight: 'calc(100vh - 58px)', background: 'linear-gradient(135deg,#f4f5f7 0%,#edeef5 100%)' }}>
        <main style={{ maxWidth: 1400, margin: '0 auto', padding: isMobile ? '16px 12px' : isTablet ? '24px 20px' : '36px 28px' }}>

          {/* Page Header */}
          <div className="anim-header" style={{ background: '#fff', borderRadius: 16, padding: isMobile ? '16px' : '22px 28px', marginBottom: isMobile ? 12 : 20, boxShadow: '0 4px 20px rgba(26,31,94,0.07)', border: '1px solid #e5e7eb' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 10 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 14, minWidth: 0 }}>
                <div style={{ width: isMobile ? 40 : 50, height: isMobile ? 40 : 50, background: 'linear-gradient(135deg,#1a1f5e,#2e3591)', borderRadius: 13, display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#e0c06a', boxShadow: '0 6px 16px rgba(26,31,94,0.2)', flexShrink: 0 }}>
                  <BarChart3 size={isMobile ? 20 : 24} />
                </div>
                <div>
                  <h1 style={{ fontSize: isMobile ? 17 : 22, fontWeight: 700, color: '#1a1f5e', fontFamily: "'Space Mono', monospace", letterSpacing: '-0.4px', margin: 0 }}>System Overview</h1>
                  <div style={{ width: 38, height: 3, background: 'linear-gradient(90deg,#c9a84c,#e0c06a)', borderRadius: 2, marginTop: 5 }} />
                </div>
              </div>
              <div className="welcome-block" style={{ textAlign: 'right' }}>
                <p style={{ fontSize: 13, color: '#6b7280', margin: 0 }}>Welcome back, {authService.getCurrentUser()?.fullName?.split(' ')[0] || 'Admin'}</p>
                <p style={{ fontSize: 12, color: '#c9a84c', fontWeight: 600, marginTop: 2 }}>Here's what's happening today</p>
              </div>
            </div>
          </div>

          {/* Search */}
          <div className="anim-search" style={{ background: '#fff', borderRadius: 13, padding: isMobile ? '12px 14px' : '14px 20px', marginBottom: isMobile ? 12 : 20, boxShadow: '0 2px 12px rgba(26,31,94,0.05)', border: '1px solid #e5e7eb' }}>
            <div style={{ position: 'relative', maxWidth: isMobile ? '100%' : 460 }}>
              <span style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)', color: '#9ca3af', pointerEvents: 'none', display: 'flex' }}><Search size={16} /></span>
              <input type="text" placeholder="Global search..." className="search-input" style={{ width: '100%', padding: '10px 12px 10px 38px', border: '2px solid #e5e7eb', borderRadius: 10, fontSize: 14, fontFamily: "'DM Sans', sans-serif", background: '#f8f9fb', color: '#1a1f5e' }} />
            </div>
          </div>

          {/* Stats */}
          <div className="stats-grid">
            {stats.map((s, i) => (
              <div key={i} className={`stat-card anim-s${i}`} style={{ background: '#fff', borderRadius: 14, padding: isMobile ? '16px' : '20px', boxShadow: '0 4px 16px rgba(26,31,94,0.06)', border: '1px solid #e5e7eb' }}>
                <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: 12 }}>
                  <span style={{ fontSize: 11, fontWeight: 600, color: '#9ca3af', textTransform: 'uppercase', letterSpacing: '0.6px', lineHeight: 1.4, paddingRight: 8 }}>{s.label}</span>
                  <div style={{ width: 36, height: 36, borderRadius: 9, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, ...iconColors[s.iconClass] }}>
                    <s.icon size={17} />
                  </div>
                </div>
                <div style={{ fontSize: isMobile ? 26 : 28, fontWeight: 700, color: '#1a1f5e', fontFamily: "'Space Mono', monospace", marginBottom: 4, lineHeight: 1 }}>{s.value}</div>
                <p style={{ fontSize: 11, fontWeight: 600, color: s.trendColor, margin: 0 }}>{s.trend}</p>
              </div>
            ))}
          </div>

          {/* Content */}
          <div className="content-grid">

            {/* Recent Activities */}
            <div className="anim-activities" style={{ background: '#fff', borderRadius: 16, padding: isMobile ? '18px 14px' : '24px', boxShadow: '0 4px 20px rgba(26,31,94,0.07)', border: '1px solid #e5e7eb' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
                <h3 style={{ fontSize: isMobile ? 14 : 17, fontWeight: 700, color: '#1a1f5e', fontFamily: "'Space Mono', monospace", margin: 0 }}>Recent Activities</h3>
                <Activity size={17} color="#c9a84c" />
              </div>
              {recentActivities.map((a, idx) => (
                <div key={a.id} className="act-item" style={{ display: 'flex', gap: 12, padding: '12px', borderRadius: 10, background: '#f8f9fb', marginBottom: idx < recentActivities.length - 1 ? 8 : 0, border: '1px solid #f0f1f3', alignItems: 'flex-start', cursor: 'default' }}>
                  <span className="a-dot" style={{ width: 8, height: 8, background: a.dot }} />
                  <div style={{ minWidth: 0 }}>
                    <p style={{ fontSize: isMobile ? 12 : 13, fontWeight: 600, color: '#1a1f5e', margin: 0, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{a.action}</p>
                    <p style={{ fontSize: 11, color: '#9ca3af', marginTop: 2 }}>{a.user} · {a.time}</p>
                  </div>
                </div>
              ))}
            </div>

            {/* Quick Actions */}
            <div className="anim-actions" style={{ background: '#fff', borderRadius: 16, padding: isMobile ? '18px 14px' : '24px', boxShadow: '0 4px 20px rgba(26,31,94,0.07)', border: '1px solid #e5e7eb' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
                <h3 style={{ fontSize: isMobile ? 14 : 17, fontWeight: 700, color: '#1a1f5e', fontFamily: "'Space Mono', monospace", margin: 0 }}>Quick Actions</h3>
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                {quickActions.map((btn, i) => (
                  <button
                    key={i}
                    className="qbtn"
                    onClick={() => {
                      btn.action();
                      // On mobile, close the sidebar
                      if (isMobile) setSidebarOpen(false);
                    }}
                    style={{
                      padding: '13px 16px',
                      borderRadius: 10,
                      background: '#f8f9fb',
                      border: '1px solid #e5e7eb',
                      color: '#1a1f5e',
                      fontFamily: "'DM Sans', sans-serif",
                      fontSize: 14,
                      fontWeight: 600,
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      width: '100%'
                    }}
                  >
                    <span>{btn.label}</span>
                    <ChevronRight size={16} color="#9ca3af" />
                  </button>
                ))}
              </div>
            </div>

          </div>
        </main>
      </div>
    </>
  );
};

export default AdminDashboard;