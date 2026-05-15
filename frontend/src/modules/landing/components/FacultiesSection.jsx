import { faculties } from "../data/landingData";
import Reveal from "./Reveal";

function FacultiesSection() {
  return (
    <section className="section-container faculties-section">
      <Reveal>
        <div className="section-header">
          <h2>University Faculties</h2>
          <p>
            Explore a wide range of faculties designed to support academic excellence,
            innovation, and future career opportunities.
          </p>
        </div>
      </Reveal>

      <div className="faculties-grid">
        {faculties.map((faculty, index) => (
          <Reveal key={index}>
            <div className="faculty-card">
              <div className="faculty-shape" />

              <div className="faculty-icon-wrap">
                <faculty.icon size={28} />
              </div>

              <h3>{faculty.title}</h3>
              <p>{faculty.desc}</p>
            </div>
          </Reveal>
        ))}
      </div>
    </section>
  );
}

export default FacultiesSection;