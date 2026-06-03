import { Users, GraduationCap, UserCog, Shield, Briefcase, BookOpen, UserCheck } from 'lucide-react';
import i18n from '../i18n/i18n';

export const USER_TYPES_CONFIG = {
  'Student': {
    label: 'طالب',
    labelEn: 'Student',
    category: 'student',
    color: '#2563eb',
    bgColor: 'rgba(37, 99, 235, 0.1)',
    icon: GraduationCap,
    order: 1
  },
  'Professor': {
    label: 'أستاذ',
    labelEn: 'Professor',
    category: 'staff',
    color: '#7c3aed',
    bgColor: 'rgba(124, 58, 237, 0.1)',
    icon: UserCog,
    order: 2
  },
  'AssistantProfessor': {
    label: 'أستاذ مساعد',
    labelEn: 'Assistant Professor',
    category: 'staff',
    color: '#8b5cf6',
    bgColor: 'rgba(139, 92, 246, 0.1)',
    icon: UserCog,
    order: 3
  },
  'TeachingAssistant': {
    label: 'معيد',
    labelEn: 'Teaching Assistant',
    category: 'staff',
    color: '#a78bfa',
    bgColor: 'rgba(167, 139, 250, 0.1)',
    icon: BookOpen,
    order: 4
  },
  'Instructor': {
    label: 'مدرس',
    labelEn: 'Instructor',
    category: 'staff',
    color: '#c084fc',
    bgColor: 'rgba(192, 132, 252, 0.1)',
    icon: Users,
    order: 5
  },
  'AdminStaff': {
    label: 'موظف إداري',
    labelEn: 'Admin Staff',
    category: 'admin',
    color: '#f59e0b',
    bgColor: 'rgba(245, 158, 11, 0.1)',
    icon: Briefcase,
    order: 6
  },
  'HR': {
    label: 'موارد بشرية',
    labelEn: 'HR',
    category: 'admin',
    color: '#ef4444',
    bgColor: 'rgba(239, 68, 68, 0.1)',
    icon: Users,
    order: 7
  },
  'SystemAdmin': {
    label: 'مدير نظام',
    labelEn: 'System Admin',
    category: 'super_admin',
    color: '#c9a84c',
    bgColor: 'rgba(201, 168, 76, 0.15)',
    icon: Shield,
    order: 8
  },
  'AcademicAdmin': {
    label: 'إدارة أكاديمية',
    labelEn: 'Academic Admin',
    category: 'admin',
    color: '#10b981',
    bgColor: 'rgba(16, 185, 129, 0.1)',
    icon: UserCheck,
    order: 9
  }
};

export const getLocalizedUserTypeLabel = (type) => {
  const config = USER_TYPES_CONFIG[type];
  if (!config) return type || 'Unknown';
  const lang = i18n.language;
  return lang === 'ar' ? config.label : config.labelEn;
};

export const getUserTypeConfig = (type) => {
  const config = USER_TYPES_CONFIG[type];
  if (!config) {
    return {
      labelEn: type || 'Unknown',
      label: type || 'غير محدد',
      localizedLabel: type || 'Unknown',
      category: 'other',
      color: '#6b7280',
      bgColor: 'rgba(107, 114, 128, 0.1)',
      icon: Users,
      order: 999
    };
  }
  const lang = i18n.language;
  return {
    ...config,
    localizedLabel: lang === 'ar' ? config.label : config.labelEn
  };
};

export const getMainCategory = (type) => {
  const config = getUserTypeConfig(type);
  return config.category;
};

export const getAllUserTypes = () => {
  return Object.values(USER_TYPES_CONFIG).sort((a, b) => a.order - b.order);
};