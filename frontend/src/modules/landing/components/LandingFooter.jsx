import { Mail, Phone, MapPin, Globe, ExternalLink, ArrowUp } from "lucide-react";

function LandingFooter() {
  const scrollToTop = () => window.scrollTo({ top: 0, behavior: "smooth" });

  return (
    <footer className="landing-footer">
      <div className="footer-container">
        <div className="footer-grid">
          <div className="footer-col brand">
            <h2 className="footer-logo">CAPU</h2>
            <p className="footer-tagline">
              Capital University — shaping future leaders through innovation, research, and academic excellence since 1995.
            </p>
            <div className="footer-social">
              <a href="#" className="social-link" aria-label="Facebook"><Globe size={18} /></a>
              <a href="#" className="social-link" aria-label="Twitter"><Globe size={18} /></a>
              <a href="#" className="social-link" aria-label="LinkedIn"><Globe size={18} /></a>
              <a href="#" className="social-link" aria-label="Instagram"><Globe size={18} /></a>
            </div>
          </div>

          <div className="footer-col">
            <h3>Quick Links</h3>
            <ul className="footer-links">
              <li><a href="/">Home</a></li>
              <li><a href="#about">About Us</a></li>
              <li><a href="#faculties">Faculties</a></li>
              <li><a href="#programs">Programs</a></li>
              <li><a href="#admissions">Admissions</a></li>
              <li><a href="#research">Research</a></li>
            </ul>
          </div>

          <div className="footer-col">
            <h3>Student Services</h3>
            <ul className="footer-links">
              <li><a href="/student/dashboard">Student Portal</a></li>
              <li><a href="#library">Library</a></li>
              <li><a href="#campus">Campus Life</a></li>
              <li><a href="#career">Career Center</a></li>
              <li><a href="#scholarships">Scholarships</a></li>
              <li><a href="#support">IT Support</a></li>
            </ul>
          </div>

          <div className="footer-col">
            <h3>Contact Us</h3>
            <ul className="footer-contact">
              <li>
                <MapPin size={14} />
                <span>123 University Avenue, Capital City</span>
              </li>
              <li>
                <Phone size={14} />
                <span>+1 (555) 123-4567</span>
              </li>
              <li>
                <Mail size={14} />
                <span>info@capitaluniversity.edu</span>
              </li>
            </ul>
          </div>
        </div>

        <div className="footer-bottom">
          <p>&copy; {new Date().getFullYear()} Capital University. All rights reserved.</p>
          <div className="footer-bottom-links">
            <a href="#privacy">Privacy Policy</a>
            <a href="#terms">Terms of Service</a>
            <a href="#accessibility">Accessibility</a>
          </div>
        </div>
      </div>

      <button className="footer-back-to-top" onClick={scrollToTop} aria-label="Back to top">
        <ArrowUp size={20} />
      </button>
    </footer>
  );
}

export default LandingFooter;
