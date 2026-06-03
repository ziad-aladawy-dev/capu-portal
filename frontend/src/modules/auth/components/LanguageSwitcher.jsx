import { useTranslation } from "react-i18next";
import { useEffect } from "react";
import "../styles/LanguageSwitcher.css";

function LanguageSwitcher() {
  const { i18n } = useTranslation();
  const currentLanguage = i18n.language;

  useEffect(() => {
    if (currentLanguage === 'ar') {
      document.body.classList.add('rtl');
      document.body.dir = 'rtl';
      document.documentElement.lang = 'ar';
    } else {
      document.body.classList.remove('rtl');
      document.body.dir = 'ltr';
      document.documentElement.lang = 'en';
    }
  }, [currentLanguage]);

  const changeLanguage = (lng) => {
    i18n.changeLanguage(lng);
    localStorage.setItem('i18nextLng', lng);
  };

  return (
    <div className="language-switcher">
      <button
        className={`lang-btn ${currentLanguage === 'ar' ? 'active' : ''}`}
        onClick={() => changeLanguage('ar')}
      >
        العربية
      </button>
      <button
        className={`lang-btn ${currentLanguage === 'en' ? 'active' : ''}`}
        onClick={() => changeLanguage('en')}
      >
        English
      </button>
    </div>
  );
}

export default LanguageSwitcher;