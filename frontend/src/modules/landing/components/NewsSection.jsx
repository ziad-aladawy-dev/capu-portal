import { useTranslation } from "react-i18next";
import { Newspaper, ArrowRight } from "lucide-react";
import { news } from "../data/landingData";
import Reveal from "./Reveal";

function NewsSection() {
  const { t } = useTranslation();

  return (
    <section className="section-container news-section">
      <Reveal>
        <div className="section-header">
          <h2>{t("landing.news.title")}</h2>
          <p>{t("landing.news.subtitle")}</p>
        </div>
      </Reveal>

      <div className="news-grid">
        {news.map((item, index) => {
          const key = `item${index + 1}`;
          return (
            <Reveal key={index}>
              <div className="news-card">
                <div className="news-icon">
                  <Newspaper size={24} />
                </div>
                <p className="news-date">{t(`landing.news.${key}.date`)}</p>
                <h3>{t(`landing.news.${key}.title`)}</h3>
                <p className="news-desc">{t(`landing.news.${key}.desc`)}</p>
                <div className="read-more">
                  {t("landing.news.read_more")}
                  <ArrowRight className="news-arrow" size={16} />
                </div>
              </div>
            </Reveal>
          );
        })}
      </div>
    </section>
  );
}

export default NewsSection;