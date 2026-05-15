import { Newspaper, ArrowRight } from "lucide-react";
import { news } from "../data/landingData";
import Reveal from "./Reveal";

function NewsSection() {
  return (
    <section className="section-container news-section">
      <Reveal>
        <div className="section-header">
          <h2>Latest News & Updates</h2>
          <p>
            Stay informed with university announcements, important updates, and events
            happening across campus.
          </p>
        </div>
      </Reveal>

      <div className="news-grid">
        {news.map((item, index) => (
          <Reveal key={index}>
            <div className="news-card">
              <div className="news-icon">
                <Newspaper size={24} />
              </div>

              <p className="news-date">{item.date}</p>

              <h3>{item.title}</h3>

              <p className="news-desc">{item.desc}</p>

              <div className="read-more">
                Read More
                <ArrowRight className="news-arrow" size={16} />
              </div>
            </div>
          </Reveal>
        ))}
      </div>
    </section>
  );
}

export default NewsSection;