import { useTranslation } from "react-i18next";
import LandingNavbar from "../components/LandingNavbar";
import HeroSlider from "../components/HeroSlider";
import StatsSection from "../components/StatsSection";
import FacultiesSection from "../components/FacultiesSection";
import ServicesSection from "../components/ServicesSection";
import NewsSection from "../components/NewsSection";
import CTASection from "../components/CTASection";
import LandingFooter from "../components/LandingFooter";
import "../styles/landing.css";

function LandingPage() {
  const { t } = useTranslation();
  return (
    <div className="landing-page" dir={t("dir", { defaultValue: "ltr" })}>
      <LandingNavbar />
      <HeroSlider />
      <StatsSection />
      <FacultiesSection />
      <ServicesSection />
      <NewsSection />
      <CTASection />
      <LandingFooter />
    </div>
  );
}

export default LandingPage;