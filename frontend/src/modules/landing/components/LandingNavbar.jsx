import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Menu, X } from "lucide-react";

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

  const closeMobileMenu = () => {
    setMobileMenuOpen(false);
  };

  const goToLogin = () => {
    navigate("/admin/login");
    closeMobileMenu();
  };

  return (
    <header className={`landing-navbar ${scrolled ? "scrolled" : ""}`}>
      <div className="navbar-container">
        <div className="navbar-brand">
          <img
            src="/images/capital-uni-logo-nobackground.png"
            alt="Capital University"
            className="navbar-logo-img"
          />
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
          <button className="login-btn" onClick={goToLogin}>
            Login
          </button>
        )}

        {isMobile && (
          <button
            className="mobile-menu-btn"
            onClick={() => setMobileMenuOpen(true)}
          >
            <Menu size={22} />
          </button>
        )}
      </div>

      {isMobile && mobileMenuOpen && (
        <div className="mobile-menu">
          <div className="mobile-menu-header">
            <span>Menu</span>

            <button onClick={closeMobileMenu}>
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

            <button onClick={goToLogin}>Login</button>
          </div>
        </div>
      )}
    </header>
  );
}

export default LandingNavbar;