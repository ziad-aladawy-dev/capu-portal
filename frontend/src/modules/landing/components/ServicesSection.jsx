import { services } from "../data/landingData";
import Reveal from "./Reveal";

function ServicesSection() {
  return (
    <section className="section-container services-section">
      <Reveal>
        <div className="services-box">
          <div className="section-header">
            <h2>Student Services</h2>
            <p>
              Access important student resources, academic tools, and digital services
              through one connected university experience.
            </p>
          </div>

          <div className="services-grid">
            {services.map((service, index) => (
              <div className="service-item" key={index}>
                <div className="service-icon">
                  <service.icon size={24} />
                </div>

                <h4>{service.title}</h4>
              </div>
            ))}
          </div>
        </div>
      </Reveal>
    </section>
  );
}

export default ServicesSection;