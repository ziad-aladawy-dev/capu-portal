import React from 'react';
import { useTranslation } from 'react-i18next';
import './Footer.css';

const Footer = () => {
  const { t } = useTranslation();

  return (
    <footer className="app-footer">
      <p>© {new Date().getFullYear()} {t("landing.footer.copyright")}</p>
    </footer>
  );
};

export default Footer;