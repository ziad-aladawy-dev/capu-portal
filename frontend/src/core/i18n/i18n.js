import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';

// استيراد ملفات الترجمة المنفصلة
import enCommon from './locales/en/common.json';
import enAuth from './locales/en/auth.json';
import enNavigation from './locales/en/navigation.json';
import enDashboard from './locales/en/dashboard.json';
import enLanding from './locales/en/landing.json';
import enStudents from './locales/en/students.json';
import enStaff from './locales/en/staff.json';
import enStructure from './locales/en/structure.json';
import enStudentServices from './locales/en/studentServices.json';
import enNotifications from './locales/en/notifications.json';
import enPermissions from './locales/en/permissions.json';
import enValidation from './locales/en/validation.json';
import enTreasury from './locales/en/treasury.json';

import arCommon from './locales/ar/common.json';
import arAuth from './locales/ar/auth.json';
import arNavigation from './locales/ar/navigation.json';
import arDashboard from './locales/ar/dashboard.json';
import arLanding from './locales/ar/landing.json';
import arStudents from './locales/ar/students.json';
import arStaff from './locales/ar/staff.json';
import arStructure from './locales/ar/structure.json';
import arStudentServices from './locales/ar/studentServices.json';
import arNotifications from './locales/ar/notifications.json';
import arPermissions from './locales/ar/permissions.json';
import arValidation from './locales/ar/validation.json';
import arTreasury from './locales/ar/treasury.json';

const resources = {
  en: {
    translation: {
      ...enCommon,
      ...enAuth,
      ...enNavigation,
      ...enDashboard,
      ...enLanding,
      ...enStudents,
      ...enStaff,
      ...enStructure,
      ...enStudentServices,
      ...enNotifications,
      ...enPermissions,
      ...enValidation,
      ...enTreasury
    }
  },
  ar: {
    translation: {
      ...arCommon,
      ...arAuth,
      ...arNavigation,
      ...arDashboard,
      ...arLanding,
      ...arStudents,
      ...arStaff,
      ...arStructure,
      ...arStudentServices,
      ...arNotifications,
      ...arPermissions,
      ...arValidation,
      ...arTreasury
    }
  }
};

i18n.on('languageChanged', (lng) => {
  document.documentElement.lang = lng;
  document.documentElement.dir = lng === 'ar' ? 'rtl' : 'ltr';
});

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    fallbackLng: 'ar',
    debug: false,
    interpolation: {
      escapeValue: false,
    },
    detection: {
      order: ['querystring', 'cookie', 'localStorage', 'navigator'],
      caches: ['localStorage'],
      lookupQuerystring: 'lng',
      lookupCookie: 'i18next',
      lookupLocalStorage: 'i18nextLng'
    }
  });

export default i18n;
