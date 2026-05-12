import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { GraduationCap, Menu, X } from "lucide-react";

function LandingNavbar() {
  const navigate = useNavigate();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [scrolled, setScrolled] = useState(false);
  const [windowWidth, setWindowWidth] = useState(window.innerWidth);

  const isMobile = windowWidth < 768;

  useEffect(() => {
    const handleResize = () => setWindowWidth(window.innerWidth);
    const handleScroll = () => setScrolled(window.scrollY > 20);

    window.addEventListener("resize", handleResize);
    window.addEventListener("scroll", handleScroll);

    return () => {
      window.removeEventListener("resize", handleResize);
      window.removeEventListener("scroll", handleScroll);
    };
  }, []);

  return (
    <header className={scrolled ? "landing-navbar scrolled" : "landing-navbar"}>
      <div className="navbar-container">
        <div className="navbar-brand">
          <div className="navbar-logo">
            <GraduationCap size={26} />
          </div>

          <div>
            <h2>Capital University</h2>
            <p>Official University Portal</p>
          </div>
        </div>

        {!isMobile && (
          <nav className="navbar-links">
            <span>Home</span>
            <span>About</span>
            <span>Faculties</span>
            <span>Admissions</span>
            <span>Services</span>
            <span>News</span>
            <span>Contact</span>
          </nav>
        )}

        {!isMobile && (
          <button className="login-btn" onClick={() => navigate("/admin/login")}>
            Login
          </button>
        )}

        {isMobile && (
          <button className="mobile-menu-btn" onClick={() => setMobileMenuOpen(true)}>
            <Menu size={22} />
          </button>
        )}
      </div>

      {isMobile && mobileMenuOpen && (
        <div className="mobile-menu">
          <div className="mobile-menu-header">
            <span>Menu</span>
            <button onClick={() => setMobileMenuOpen(false)}>
              <X size={18} />
            </button>
          </div>

          <div className="mobile-menu-links">
            <span>Home</span>
            <span>About</span>
            <span>Faculties</span>
            <span>Admissions</span>
            <span>Services</span>
            <span>News</span>
            <span>Contact</span>

            <button onClick={() => navigate("/admin/login")}>Login</button>
          </div>
        </div>
      )}
    </header>
  );
}

export default LandingNavbar;