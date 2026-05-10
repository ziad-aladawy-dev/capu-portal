import React from 'react';
import { Users, UserCheck, UserX, GraduationCap, Briefcase } from 'lucide-react';

const UserStats = ({ statistics, loading }) => {
  if (loading) {
    return (
      <div style={{
        background: 'var(--pure-white)',
        borderRadius: '16px',
        padding: '20px',
        marginBottom: '24px',
        textAlign: 'center',
        color: 'var(--text-muted)'
      }}>
        Loading statistics...
      </div>
    );
  }

  if (!statistics) return null;

  const statsContainerStyle = {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
    gap: '16px',
    marginBottom: '24px'
  };

  const statCardStyle = {
    background: 'var(--pure-white)',
    borderRadius: '16px',
    padding: '20px',
    boxShadow: '0 4px 20px rgba(26, 31, 94, 0.06)',
    border: '1px solid var(--border-color)',
    transition: 'all 0.3s',
    textAlign: 'center'
  };

  const statHeaderStyle = {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: '12px'
  };

  const statLabelStyle = {
    fontSize: '13px',
    fontWeight: '600',
    color: 'var(--text-muted)',
    textTransform: 'uppercase',
    letterSpacing: '0.5px'
  };

  const statIconStyle = (bg, color) => ({
    width: '40px',
    height: '40px',
    borderRadius: '12px',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    background: bg,
    color: color
  });

  const statValueStyle = {
    fontSize: '28px',
    fontWeight: '700',
    color: 'var(--navy-primary)',
    fontFamily: "'Space Mono', monospace",
    lineHeight: '1.2'
  };

  const stats = [
    {
      label: 'Total Users',
      value: statistics.totalUsers || 0,
      icon: Users,
      bg: 'rgba(26,31,94,0.1)',
      color: 'var(--navy-primary)'
    },
    {
      label: 'Active Users',
      value: statistics.activeUsers || 0,
      icon: UserCheck,
      bg: 'rgba(22,163,74,0.1)',
      color: 'var(--success-green)'
    },
    {
      label: 'Inactive Users',
      value: statistics.inactiveUsers || 0,
      icon: UserX,
      bg: 'rgba(220,38,38,0.1)',
      color: 'var(--warning-red)'
    },
    {
      label: 'Students',
      value: statistics.studentsCount || 0,
      icon: GraduationCap,
      bg: 'rgba(37,99,235,0.1)',
      color: '#2563eb'
    },
    {
      label: 'Staff',
      value: statistics.staffCount || 0,
      icon: Briefcase,
      bg: 'rgba(124,58,237,0.1)',
      color: '#7c3aed'
    }
  ];

  return (
    <div style={statsContainerStyle}>
      {stats.map((stat, index) => (
        <div 
          key={index} 
          style={statCardStyle}
          onMouseEnter={(e) => {
            e.currentTarget.style.transform = 'translateY(-4px)';
            e.currentTarget.style.boxShadow = '0 8px 30px rgba(26, 31, 94, 0.12)';
          }}
          onMouseLeave={(e) => {
            e.currentTarget.style.transform = 'translateY(0)';
            e.currentTarget.style.boxShadow = '0 4px 20px rgba(26, 31, 94, 0.06)';
          }}
        >
          <div style={statHeaderStyle}>
            <span style={statLabelStyle}>{stat.label}</span>
            <div style={statIconStyle(stat.bg, stat.color)}>
              <stat.icon size={18} />
            </div>
          </div>
          <div style={statValueStyle}>{stat.value.toLocaleString()}</div>
        </div>
      ))}
    </div>
  );
};

export default UserStats;