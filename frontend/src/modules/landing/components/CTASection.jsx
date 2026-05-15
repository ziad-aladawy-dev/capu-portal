import { useNavigate } from "react-router-dom";
import { GraduationCap, ArrowRight } from "lucide-react";
import AdminLogin from "../../../core/auth/pages/AdminLogin";
import Reveal from "./Reveal";

function CTASection() {
  const navigate = useNavigate();

  return (
    <section className="section-container cta-section">
      <Reveal>
        <div className="cta-box">
          <div className="floating-shape cta-shape-one" />
          <div className="floating-shape delay-2 cta-shape-two" />

          <div className="cta-icon">
            <GraduationCap size={32} />
          </div>

          <h2>Ready to Explore Capital University?</h2>

          <p>
            Access the university portal and discover academic services, student support,
            campus resources, and a modern digital experience designed for everyone.
          </p>

          <button className="primary-btn" onClick={() => navigate("/admin/login")}>
            Go to Login
            <ArrowRight size={18} />
          </button>
        </div>
      </Reveal>
    </section>
  );
}

export default CTASection;