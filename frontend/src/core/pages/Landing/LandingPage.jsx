import React, { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import uni1 from '../../../assets/images/University1.png';
import uni2 from '../../../assets/images/University2.png';
import uni3 from '../../../assets/images/University3.png';
import {
  GraduationCap,
  Building2,
  BookOpen,
  Users,
  ChevronLeft,
  ChevronRight,
  Menu,
  X,
  CalendarDays,
  FileText,
  BadgeHelp,
  MonitorSmartphone,
  Newspaper,
  MapPin,
  ArrowRight,
  Library,
  Cpu,
  Briefcase,
  HeartPulse,
  FlaskConical
} from 'lucide-react';

const CountUp = ({ end, suffix = '' }) => {
  const [count, setCount] = useState(0);
  const ref = useRef(null);
  const [started, setStarted] = useState(false);

  useEffect(() => {
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) setStarted(true);
      },
      { threshold: 0.3 }
    );

    if (ref.current) observer.observe(ref.current);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!started) return;

    let start = 0;
    const duration = 1800;
    const increment = end / (duration / 16);

    const timer = setInterval(() => {
      start += increment;
      if (start >= end) {
        setCount(end);
        clearInterval(timer);
      } else {
        setCount(Math.floor(start));
      }
    }, 16);

    return () => clearInterval(timer);
  }, [started, end]);

  return (
    <span ref={ref}>
      {count.toLocaleString()}
      {suffix}
    </span>
  );
};

const Reveal = ({ children }) => {
  const ref = useRef(null);
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) setVisible(true);
      },
      { threshold: 0.15 }
    );

    if (ref.current) observer.observe(ref.current);
    return () => observer.disconnect();
  }, []);

  return (
    <div
      ref={ref}
      style={{
        opacity: visible ? 1 : 0,
        transform: visible ? 'translateY(0)' : 'translateY(35px)',
        transition: 'all 0.8s ease'
      }}
    >
      {children}
    </div>
  );
};

const LandingPage = () => {
  const navigate = useNavigate();

  const [currentSlide, setCurrentSlide] = useState(0);
  const [isTransitioning, setIsTransitioning] = useState(true);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [windowWidth, setWindowWidth] = useState(
    typeof window !== 'undefined' ? window.innerWidth : 1200
  );
  const [scrolled, setScrolled] = useState(false);

  const slides = [
    {
      image: uni1,
      subtitle: 'Welcome to Capital University',
      title: 'A Modern University Experience',
      description:
        'Discover a university environment built for learning, innovation, research, and student success.',
      buttonText: 'Get Started'
    },
    {
      image: uni2,
      subtitle: 'Academic Excellence',
      title: 'Faculties, Programs, and Opportunities',
      description:
        'Explore diverse faculties, modern academic programs, and a future-ready educational journey.',
      buttonText: 'Get Started'
    },
    {
      image: uni3,
      subtitle: 'Student Life & Services',
      title: 'Everything Students Need in One Place',
      description:
        'Access academic support, digital services, university updates, campus resources, and more.',
      buttonText: 'Get Started'
    }
  ];

  const sliderSlides = [...slides, slides[0]];

  const faculties = [
    {
      title: 'Faculty of Computer Science',
      icon: Cpu,
      desc: 'Programs focused on software, artificial intelligence, data, and modern digital systems.'
    },
    {
      title: 'Faculty of Engineering',
      icon: Building2,
      desc: 'Practical and theoretical education across multiple engineering disciplines.'
    },
    {
      title: 'Faculty of Business',
      icon: Briefcase,
      desc: 'Preparing future professionals in business, management, accounting, and finance.'
    },
    {
      title: 'Faculty of Pharmacy',
      icon: HeartPulse,
      desc: 'Supporting healthcare education through science, knowledge, and professional practice.'
    },
    {
      title: 'Faculty of Science',
      icon: FlaskConical,
      desc: 'Building strong scientific foundations through research, labs, and discovery.'
    },
    {
      title: 'Faculty of Arts',
      icon: BookOpen,
      desc: 'Encouraging creativity, communication, culture, and critical thinking.'
    }
  ];

  const services = [
    { title: 'Academic Calendar', icon: CalendarDays },
    { title: 'Forms & Documents', icon: FileText },
    { title: 'Student Guide', icon: BadgeHelp },
    { title: 'E-Learning', icon: MonitorSmartphone },
    { title: 'Library', icon: Library },
    { title: 'Campus Services', icon: MapPin }
  ];

  const stats = [
    { label: 'Students', value: 18000, suffix: '+', icon: GraduationCap },
    { label: 'Faculties', value: 12, suffix: '', icon: Building2 },
    { label: 'Programs', value: 60, suffix: '+', icon: BookOpen },
    { label: 'Staff Members', value: 1200, suffix: '+', icon: Users }
  ];

  const news = [
    {
      title: 'Admission for the new academic year is now open',
      desc: 'Applications are now available for undergraduate and postgraduate programs.',
      date: 'Latest Update'
    },
    {
      title: 'New digital services launched for students',
      desc: 'Students can now access more academic resources and online support services.',
      date: 'University News'
    },
    {
      title: 'Upcoming campus activities and events',
      desc: 'Stay updated with workshops, seminars, and student engagement activities.',
      date: 'Events'
    }
  ];

  useEffect(() => {
    const handleResize = () => setWindowWidth(window.innerWidth);
    const handleScroll = () => setScrolled(window.scrollY > 20);

    window.addEventListener('resize', handleResize);
    window.addEventListener('scroll', handleScroll);

    const interval = setInterval(() => {
      setCurrentSlide((prev) => prev + 1);
      setIsTransitioning(true);
    }, 4500);

    return () => {
      window.removeEventListener('resize', handleResize);
      window.removeEventListener('scroll', handleScroll);
      clearInterval(interval);
    };
  }, []);

  const isMobile = windowWidth < 768;
  const isTablet = windowWidth >= 768 && windowWidth < 1100;

  const nextSlide = () => {
    setCurrentSlide((prev) => prev + 1);
    setIsTransitioning(true);
  };

  const prevSlide = () => {
    if (currentSlide === 0) {
      setIsTransitioning(false);
      setCurrentSlide(slides.length - 1);
    } else {
      setIsTransitioning(true);
      setCurrentSlide((prev) => prev - 1);
    }
  };

  const activeDot = currentSlide % slides.length;

  return (
    <>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Space+Mono:wght@400;700&family=DM+Sans:wght@400;500;600;700&display=swap');

        * {
          margin: 0;
          padding: 0;
          box-sizing: border-box;
        }

        html, body {
          font-family: 'DM Sans', sans-serif;
          background: #f4f5f7;
          overflow-x: hidden;
          scroll-behavior: smooth;
        }

        @keyframes floatY {
          0%, 100% { transform: translateY(0px); }
          50% { transform: translateY(-18px); }
        }

        @keyframes pulseGlow {
          0%, 100% { opacity: 0.35; transform: scale(1); }
          50% { opacity: 0.7; transform: scale(1.08); }
        }

        @keyframes slideText {
          from {
            opacity: 0;
            transform: translateY(30px);
          }
          to {
            opacity: 1;
            transform: translateY(0);
          }
        }

        .nav-link {
          position: relative;
          color: white;
          font-size: 15px;
          font-weight: 600;
          cursor: pointer;
          transition: 0.25s ease;
        }

        .nav-link:hover {
          color: #e0c06a;
        }

        .nav-link::after {
          content: '';
          position: absolute;
          left: 0;
          bottom: -8px;
          width: 0;
          height: 2px;
          background: #e0c06a;
          transition: 0.25s ease;
        }

        .nav-link:hover::after {
          width: 100%;
        }

        .primary-btn,
        .secondary-btn,
        .slider-arrow,
        .card-hover,
        .service-item,
        .faculty-card,
        .news-card {
          transition: all 0.3s ease;
        }

        .primary-btn:hover {
          transform: translateY(-3px);
          box-shadow: 0 14px 26px rgba(224,192,106,0.28);
        }

        .secondary-btn:hover {
          transform: translateY(-3px);
          background: rgba(255,255,255,0.12);
        }

        .slider-arrow:hover {
          transform: translateY(-50%) scale(1.08);
          background: #ffffff !important;
        }

        .card-hover:hover,
        .faculty-card:hover,
        .news-card:hover {
          transform: translateY(-8px);
          box-shadow: 0 18px 38px rgba(26,31,94,0.12);
        }

        .service-item:hover {
          transform: translateY(-6px);
          background: #fdf6e3 !important;
          border-color: rgba(201,168,76,0.35) !important;
          box-shadow: 0 14px 28px rgba(201,168,76,0.15);
        }

        .slider-content {
          animation: slideText 0.85s ease;
        }

        .floating-shape {
          animation: floatY 6s ease-in-out infinite;
        }

        .floating-shape.delay-2 {
          animation-delay: 1.5s;
        }

        .floating-shape.delay-3 {
          animation-delay: 3s;
        }

        .pulse-shape {
          animation: pulseGlow 4s ease-in-out infinite;
        }

        .dot {
          transition: all 0.25s ease;
        }

        .dot.active {
          width: 30px !important;
          background: #e0c06a !important;
        }

        .faculty-icon-wrap {
          transition: all 0.35s ease;
        }

        .faculty-card:hover .faculty-icon-wrap {
          transform: scale(1.08) rotate(4deg);
          box-shadow: 0 14px 30px rgba(26,31,94,0.18);
        }

        .news-card:hover .news-arrow {
          transform: translateX(6px);
        }
      `}</style>

      <div style={{ minHeight: '100vh', background: 'linear-gradient(135deg,#f4f5f7 0%,#edeef5 100%)' }}>
        <header
          style={{
            position: 'fixed',
            top: 0,
            left: 0,
            width: '100%',
            zIndex: 1000,
            background: scrolled ? 'rgba(26,31,94,0.95)' : 'transparent',
            backdropFilter: scrolled ? 'blur(10px)' : 'none',
            boxShadow: scrolled ? '0 6px 18px rgba(26,31,94,0.14)' : 'none',
            transition: 'all 0.35s ease'
          }}
        >
          <div
            style={{
              maxWidth: 1400,
              margin: '0 auto',
              padding: isMobile ? '14px 16px' : '16px 28px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: 20
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
              <div
                style={{
                  width: 52,
                  height: 52,
                  borderRadius: 14,
                  background: scrolled ? 'rgba(255,255,255,0.12)' : 'rgba(255,255,255,0.16)',
                  color: '#e0c06a',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  border: '1px solid rgba(255,255,255,0.15)',
                  flexShrink: 0
                }}
              >
                <GraduationCap size={26} />
              </div>

              <div>
                <h2
                  style={{
                    color: '#fff',
                    fontSize: isMobile ? 16 : 20,
                    fontWeight: 700,
                    fontFamily: "'Space Mono', monospace"
                  }}
                >
                  Capital University
                </h2>
                <p style={{ color: '#e5e7eb', fontSize: 12, marginTop: 2 }}>
                  Official University Portal
                </p>
              </div>
            </div>

            {!isMobile && (
              <nav style={{ display: 'flex', alignItems: 'center', gap: 28 }}>
                <span className="nav-link">Home</span>
                <span className="nav-link">About</span>
                <span className="nav-link">Faculties</span>
                <span className="nav-link">Admissions</span>
                <span className="nav-link">Services</span>
                <span className="nav-link">News</span>
                <span className="nav-link">Contact</span>
              </nav>
            )}

            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
{!isMobile && (
  <button
    onClick={() => navigate('/login')}
    style={{
      padding: '11px 22px',
      borderRadius: 12,
      border: 'none',
      background: 'linear-gradient(135deg, #e0c06a 0%, #c9a84c 100%)',
      color: '#1a1f5e',
      fontWeight: 700,
      fontSize: 14,
      cursor: 'pointer',
      boxShadow: '0 10px 22px rgba(224,192,106,0.28)',
      transition: 'all 0.3s ease'
    }}
    onMouseEnter={(e) => {
      e.currentTarget.style.transform = 'translateY(-2px) scale(1.02)';
      e.currentTarget.style.boxShadow = '0 14px 26px rgba(224,192,106,0.35)';
    }}
    onMouseLeave={(e) => {
      e.currentTarget.style.transform = 'translateY(0) scale(1)';
      e.currentTarget.style.boxShadow = '0 10px 22px rgba(224,192,106,0.28)';
    }}
  >
    Login
  </button>
)}
         {isMobile && (
                <button
                  onClick={() => setMobileMenuOpen(true)}
                  style={{
                    width: 42,
                    height: 42,
                    borderRadius: 10,
                    border: '1px solid rgba(255,255,255,0.18)',
                    background: 'rgba(255,255,255,0.08)',
                    color: '#fff',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    cursor: 'pointer'
                  }}
                >
                  <Menu size={22} />
                </button>
              )}
            </div>
          </div>

          {isMobile && mobileMenuOpen && (
            <div
              style={{
                background: '#1e256d',
                borderTop: '1px solid rgba(255,255,255,0.08)',
                padding: '16px'
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
                <span style={{ color: '#fff', fontWeight: 700 }}>Menu</span>
                <button
                  onClick={() => setMobileMenuOpen(false)}
                  style={{
                    width: 34,
                    height: 34,
                    border: 'none',
                    borderRadius: 8,
                    background: 'rgba(255,255,255,0.08)',
                    color: '#fff',
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center'
                  }}
                >
                  <X size={18} />
                </button>
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
                <span style={{ color: '#fff', fontWeight: 600 }}>Home</span>
                <span style={{ color: '#fff', fontWeight: 600 }}>About</span>
                <span style={{ color: '#fff', fontWeight: 600 }}>Faculties</span>
                <span style={{ color: '#fff', fontWeight: 600 }}>Admissions</span>
                <span style={{ color: '#fff', fontWeight: 600 }}>Services</span>
                <span style={{ color: '#fff', fontWeight: 600 }}>News</span>
                <span style={{ color: '#fff', fontWeight: 600 }}>Contact</span>

                <button
                  onClick={() => navigate('/login')}
                  style={{
                    marginTop: 8,
                    padding: '12px 16px',
                    borderRadius: 10,
                    border: 'none',
                    background: '#e0c06a',
                    color: '#1a1f5e',
                    fontWeight: 700,
                    cursor: 'pointer'
                  }}
                >
                  Login
                </button>
              </div>
            </div>
          )}
        </header>

        <section style={{ position: 'relative', overflow: 'hidden' }}>
          <div
            style={{
              minHeight: isMobile ? 560 : 720,
              position: 'relative',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center'
            }}
          >
            <div
              onTransitionEnd={() => {
                if (currentSlide === slides.length) {
                  setIsTransitioning(false);
                  setCurrentSlide(0);
                }
              }}
              style={{
                position: 'absolute',
                inset: 0,
                display: 'flex',
                width: `${sliderSlides.length * 100}%`,
                height: '100%',
                transform: `translateX(-${currentSlide * (100 / sliderSlides.length)}%)`,
                transition: isTransitioning ? 'transform 1.1s ease-in-out' : 'none'
              }}
            >
              {sliderSlides.map((slide, index) => (
                <div
                  key={index}
                  style={{
                    width: `${100 / sliderSlides.length}%`,
                    height: '100%',
                    flexShrink: 0,
                    position: 'relative'
                  }}
                >
                  <img
                    src={slide.image}
                    alt={slide.title}
                    style={{
                      width: '100%',
                      height: '100%',
                      objectFit: 'cover',
                      display: 'block'
                    }}
                  />

                  <div
                    style={{
                      position: 'absolute',
                      inset: 0,
                      background: 'rgba(0,0,0,0.38)'
                    }}
                  />
                </div>
              ))}
            </div>

            <div
              className="floating-shape pulse-shape"
              style={{
                position: 'absolute',
                width: isMobile ? 180 : 260,
                height: isMobile ? 180 : 260,
                borderRadius: '50%',
                background: 'rgba(224,192,106,0.08)',
                top: '14%',
                left: '8%',
                filter: 'blur(6px)',
                zIndex: 2
              }}
            />

            <div
              className="floating-shape delay-2"
              style={{
                position: 'absolute',
                width: isMobile ? 120 : 180,
                height: isMobile ? 120 : 180,
                borderRadius: '50%',
                background: 'rgba(255,255,255,0.05)',
                bottom: '14%',
                right: '10%',
                filter: 'blur(2px)',
                zIndex: 2
              }}
            />

            <div
              className="floating-shape delay-3 pulse-shape"
              style={{
                position: 'absolute',
                width: isMobile ? 90 : 140,
                height: isMobile ? 90 : 140,
                borderRadius: '50%',
                background: 'rgba(255,255,255,0.06)',
                top: '20%',
                right: '18%',
                filter: 'blur(2px)',
                zIndex: 2
              }}
            />

            <div
              style={{
                position: 'relative',
                zIndex: 3,
                width: '100%',
                maxWidth: 1200,
                margin: '0 auto',
                padding: isMobile ? '110px 18px 70px' : '140px 28px 90px',
                textAlign: 'center',
                display: 'flex',
                justifyContent: 'center'
              }}
            >
              <div className="slider-content" key={`text-${activeDot}`} style={{ maxWidth: 850 }}>
                <p
                  style={{
                    fontSize: isMobile ? 14 : 17,
                    color: '#e0c06a',
                    fontWeight: 700,
                    letterSpacing: '0.6px',
                    marginBottom: 16
                  }}
                >
                  {slides[activeDot].subtitle}
                </p>

                <h1
                  style={{
                    fontSize: isMobile ? 30 : isTablet ? 46 : 64,
                    lineHeight: 1.12,
                    color: '#fff',
                    fontWeight: 700,
                    fontFamily: "'Space Mono', monospace",
                    marginBottom: 18,
                    textShadow: '0 4px 20px rgba(0,0,0,0.35)'
                  }}
                >
                  {slides[activeDot].title}
                </h1>

                <p
                  style={{
                    fontSize: isMobile ? 15 : 18,
                    lineHeight: 1.9,
                    color: 'rgba(255,255,255,0.95)',
                    maxWidth: 760,
                    margin: '0 auto 28px',
                    textShadow: '0 4px 18px rgba(0,0,0,0.28)'
                  }}
                >
                  {slides[activeDot].description}
                </p>

                <div
                  style={{
                    display: 'flex',
                    gap: 14,
                    justifyContent: 'center',
                    flexWrap: 'wrap'
                  }}
                >
                  <button
                    className="primary-btn"
                    onClick={() => navigate('/login')}
                    style={{
                      padding: '14px 24px',
                      borderRadius: 12,
                      border: 'none',
                      background: '#e0c06a',
                      color: '#1a1f5e',
                      fontWeight: 700,
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 8
                    }}
                  >
                    {slides[activeDot].buttonText}
                    <ArrowRight size={18} />
                  </button>

                  <button
                    className="secondary-btn"
                    onClick={() => navigate('/login')}
                    style={{
                      padding: '14px 24px',
                      borderRadius: 12,
                      border: '1px solid rgba(255,255,255,0.28)',
                      background: 'rgba(255,255,255,0.08)',
                      color: '#fff',
                      fontWeight: 600,
                      cursor: 'pointer',
                      backdropFilter: 'blur(4px)'
                    }}
                  >
                    Login to Portal
                  </button>
                </div>
              </div>
            </div>

            <button
              className="slider-arrow"
              onClick={prevSlide}
              style={{
                position: 'absolute',
                left: isMobile ? 12 : 24,
                top: '50%',
                transform: 'translateY(-50%)',
                width: isMobile ? 42 : 50,
                height: isMobile ? 42 : 50,
                borderRadius: '50%',
                border: 'none',
                background: 'rgba(255,255,255,0.88)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                cursor: 'pointer',
                zIndex: 4,
                boxShadow: '0 8px 18px rgba(0,0,0,0.15)'
              }}
            >
              <ChevronLeft size={22} color="#1a1f5e" />
            </button>

            <button
              className="slider-arrow"
              onClick={nextSlide}
              style={{
                position: 'absolute',
                right: isMobile ? 12 : 24,
                top: '50%',
                transform: 'translateY(-50%)',
                width: isMobile ? 42 : 50,
                height: isMobile ? 42 : 50,
                borderRadius: '50%',
                border: 'none',
                background: 'rgba(255,255,255,0.88)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                cursor: 'pointer',
                zIndex: 4,
                boxShadow: '0 8px 18px rgba(0,0,0,0.15)'
              }}
            >
              <ChevronRight size={22} color="#1a1f5e" />
            </button>

            <div
              style={{
                position: 'absolute',
                bottom: 28,
                left: '50%',
                transform: 'translateX(-50%)',
                display: 'flex',
                alignItems: 'center',
                gap: 10,
                zIndex: 4
              }}
            >
              {slides.map((_, i) => (
                <button
                  key={i}
                  onClick={() => {
                    setCurrentSlide(i);
                    setIsTransitioning(true);
                  }}
                  className={`dot ${activeDot === i ? 'active' : ''}`}
                  style={{
                    width: 11,
                    height: 11,
                    borderRadius: 999,
                    border: 'none',
                    background: activeDot === i ? '#e0c06a' : 'rgba(255,255,255,0.75)',
                    cursor: 'pointer'
                  }}
                />
              ))}
            </div>
          </div>
        </section>

        <section style={{ maxWidth: 1400, margin: '0 auto', padding: isMobile ? '28px 16px 10px' : '46px 28px 20px' }}>
          <Reveal>
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: isMobile ? '1fr' : isTablet ? '1fr 1fr' : 'repeat(4, 1fr)',
                gap: 18
              }}
            >
              {stats.map((item, index) => (
                <div
                  key={index}
                  className="card-hover"
                  style={{
                    background: '#fff',
                    borderRadius: 18,
                    padding: '24px 20px',
                    border: '1px solid #e5e7eb',
                    boxShadow: '0 6px 18px rgba(26,31,94,0.06)'
                  }}
                >
                  <div
                    style={{
                      width: 52,
                      height: 52,
                      borderRadius: 14,
                      background: 'rgba(26,31,94,0.08)',
                      color: '#1a1f5e',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      marginBottom: 14
                    }}
                  >
                    <item.icon size={24} />
                  </div>

                  <h3
                    style={{
                      fontSize: 30,
                      color: '#1a1f5e',
                      fontWeight: 700,
                      fontFamily: "'Space Mono', monospace",
                      marginBottom: 6
                    }}
                  >
                    <CountUp end={item.value} suffix={item.suffix} />
                  </h3>

                  <p style={{ fontSize: 14, color: '#6b7280', fontWeight: 600 }}>
                    {item.label}
                  </p>
                </div>
              ))}
            </div>
          </Reveal>
        </section>

        <section style={{ maxWidth: 1400, margin: '0 auto', padding: isMobile ? '36px 16px 20px' : '56px 28px 30px' }}>
          <Reveal>
            <div style={{ textAlign: 'center', marginBottom: 30 }}>
              <h2
                style={{
                  fontSize: isMobile ? 26 : 40,
                  color: '#1a1f5e',
                  fontWeight: 700,
                  fontFamily: "'Space Mono', monospace",
                  marginBottom: 12
                }}
              >
                University Faculties
              </h2>
              <p
                style={{
                  maxWidth: 760,
                  margin: '0 auto',
                  color: '#6b7280',
                  fontSize: isMobile ? 14 : 16,
                  lineHeight: 1.9
                }}
              >
                Explore a wide range of faculties designed to support academic excellence,
                innovation, and future career opportunities.
              </p>
            </div>
          </Reveal>

          <div
            style={{
              display: 'grid',
              gridTemplateColumns: isMobile ? '1fr' : isTablet ? '1fr 1fr' : 'repeat(3, 1fr)',
              gap: 22
            }}
          >
            {faculties.map((faculty, index) => (
              <Reveal key={index}>
                <div
                  className="faculty-card"
                  style={{
                    background: '#fff',
                    borderRadius: 22,
                    padding: '26px 22px',
                    border: '1px solid #e5e7eb',
                    boxShadow: '0 6px 20px rgba(26,31,94,0.06)',
                    position: 'relative',
                    overflow: 'hidden'
                  }}
                >
                  <div
                    style={{
                      position: 'absolute',
                      top: -40,
                      right: -40,
                      width: 120,
                      height: 120,
                      borderRadius: '50%',
                      background: 'rgba(46,53,145,0.06)'
                    }}
                  />

                  <div
                    className="faculty-icon-wrap"
                    style={{
                      width: 64,
                      height: 64,
                      borderRadius: 18,
                      background: 'linear-gradient(135deg,#1a1f5e,#2e3591)',
                      color: '#e0c06a',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      marginBottom: 16,
                      position: 'relative',
                      zIndex: 2
                    }}
                  >
                    <faculty.icon size={28} />
                  </div>

                  <h3
                    style={{
                      fontSize: 19,
                      color: '#1a1f5e',
                      fontWeight: 700,
                      marginBottom: 10,
                      position: 'relative',
                      zIndex: 2
                    }}
                  >
                    {faculty.title}
                  </h3>

                  <p
                    style={{
                      fontSize: 14,
                      color: '#6b7280',
                      lineHeight: 1.8,
                      position: 'relative',
                      zIndex: 2
                    }}
                  >
                    {faculty.desc}
                  </p>
                </div>
              </Reveal>
            ))}
          </div>
        </section>

        <section style={{ maxWidth: 1400, margin: '0 auto', padding: isMobile ? '36px 16px 20px' : '52px 28px 30px' }}>
          <Reveal>
            <div
              style={{
                background: '#eef2fa',
                borderRadius: 26,
                padding: isMobile ? '24px 18px' : '34px 30px',
                border: '1px solid #dde4f2'
              }}
            >
              <div style={{ textAlign: 'center', marginBottom: 28 }}>
                <h2
                  style={{
                    fontSize: isMobile ? 26 : 40,
                    color: '#1a1f5e',
                    fontWeight: 700,
                    fontFamily: "'Space Mono', monospace",
                    marginBottom: 12
                  }}
                >
                  Student Services
                </h2>
                <p
                  style={{
                    maxWidth: 760,
                    margin: '0 auto',
                    color: '#6b7280',
                    fontSize: isMobile ? 14 : 16,
                    lineHeight: 1.9
                  }}
                >
                  Access important student resources, academic tools, and digital services
                  through one connected university experience.
                </p>
              </div>

              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: isMobile ? '1fr 1fr' : isTablet ? 'repeat(3, 1fr)' : 'repeat(6, 1fr)',
                  gap: 16
                }}
              >
                {services.map((service, index) => (
                  <div
                    key={index}
                    className="service-item"
                    style={{
                      background: '#fff',
                      borderRadius: 18,
                      padding: '22px 14px',
                      textAlign: 'center',
                      border: '1px solid #e5e7eb',
                      boxShadow: '0 4px 14px rgba(26,31,94,0.05)'
                    }}
                  >
                    <div
                      style={{
                        width: 54,
                        height: 54,
                        borderRadius: 16,
                        margin: '0 auto 14px',
                        background: 'rgba(26,31,94,0.08)',
                        color: '#1a1f5e',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center'
                      }}
                    >
                      <service.icon size={24} />
                    </div>

                    <h4
                      style={{
                        fontSize: 15,
                        color: '#1a1f5e',
                        fontWeight: 700,
                        lineHeight: 1.5
                      }}
                    >
                      {service.title}
                    </h4>
                  </div>
                ))}
              </div>
            </div>
          </Reveal>
        </section>

        <section style={{ maxWidth: 1400, margin: '0 auto', padding: isMobile ? '36px 16px 30px' : '50px 28px 40px' }}>
          <Reveal>
            <div style={{ textAlign: 'center', marginBottom: 28 }}>
              <h2
                style={{
                  fontSize: isMobile ? 26 : 40,
                  color: '#1a1f5e',
                  fontWeight: 700,
                  fontFamily: "'Space Mono', monospace",
                  marginBottom: 12
                }}
              >
                Latest News & Updates
              </h2>
              <p
                style={{
                  maxWidth: 760,
                  margin: '0 auto',
                  color: '#6b7280',
                  fontSize: isMobile ? 14 : 16,
                  lineHeight: 1.9
                }}
              >
                Stay informed with university announcements, important updates, and events
                happening across campus.
              </p>
            </div>
          </Reveal>

          <div
            style={{
              display: 'grid',
              gridTemplateColumns: isMobile ? '1fr' : isTablet ? '1fr' : 'repeat(3, 1fr)',
              gap: 20
            }}
          >
            {news.map((item, index) => (
              <Reveal key={index}>
                <div
                  className="news-card"
                  style={{
                    background: '#fff',
                    borderRadius: 20,
                    padding: '24px 20px',
                    border: '1px solid #e5e7eb',
                    boxShadow: '0 6px 18px rgba(26,31,94,0.06)'
                  }}
                >
                  <div
                    style={{
                      width: 54,
                      height: 54,
                      borderRadius: 16,
                      background: 'linear-gradient(135deg,#1a1f5e,#2e3591)',
                      color: '#e0c06a',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      marginBottom: 16
                    }}
                  >
                    <Newspaper size={24} />
                  </div>

                  <p
                    style={{
                      fontSize: 12,
                      fontWeight: 700,
                      color: '#c9a84c',
                      textTransform: 'uppercase',
                      marginBottom: 10
                    }}
                  >
                    {item.date}
                  </p>

                  <h3
                    style={{
                      fontSize: 18,
                      color: '#1a1f5e',
                      fontWeight: 700,
                      lineHeight: 1.5,
                      marginBottom: 10
                    }}
                  >
                    {item.title}
                  </h3>

                  <p
                    style={{
                      fontSize: 14,
                      color: '#6b7280',
                      lineHeight: 1.8,
                      marginBottom: 16
                    }}
                  >
                    {item.desc}
                  </p>

                  <div
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: 8,
                      color: '#1a1f5e',
                      fontWeight: 700,
                      cursor: 'pointer'
                    }}
                  >
                    Read More
                    <ArrowRight className="news-arrow" size={16} style={{ transition: '0.25s ease' }} />
                  </div>
                </div>
              </Reveal>
            ))}
          </div>
        </section>

        <section style={{ maxWidth: 1400, margin: '0 auto', padding: isMobile ? '20px 16px 50px' : '30px 28px 80px' }}>
          <Reveal>
            <div
              style={{
                background: 'linear-gradient(135deg,#1a1f5e 0%, #2e3591 100%)',
                borderRadius: 26,
                padding: isMobile ? '30px 18px' : '46px 34px',
                textAlign: 'center',
                color: '#fff',
                boxShadow: '0 16px 38px rgba(26,31,94,0.18)',
                position: 'relative',
                overflow: 'hidden'
              }}
            >
              <div
                className="floating-shape"
                style={{
                  position: 'absolute',
                  width: 180,
                  height: 180,
                  borderRadius: '50%',
                  background: 'rgba(224,192,106,0.1)',
                  top: -60,
                  right: -40
                }}
              />

              <div
                className="floating-shape delay-2"
                style={{
                  position: 'absolute',
                  width: 140,
                  height: 140,
                  borderRadius: '50%',
                  background: 'rgba(255,255,255,0.06)',
                  bottom: -40,
                  left: -30
                }}
              />

              <div
                style={{
                  width: 72,
                  height: 72,
                  borderRadius: '50%',
                  margin: '0 auto 18px',
                  background: 'rgba(224,192,106,0.14)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: '#e0c06a',
                  position: 'relative',
                  zIndex: 2
                }}
              >
                <GraduationCap size={32} />
              </div>

              <h2
                style={{
                  fontSize: isMobile ? 26 : 40,
                  fontWeight: 700,
                  fontFamily: "'Space Mono', monospace",
                  marginBottom: 14,
                  position: 'relative',
                  zIndex: 2
                }}
              >
                Ready to Explore Capital University?
              </h2>

              <p
                style={{
                  fontSize: isMobile ? 14 : 16,
                  color: 'rgba(255,255,255,0.85)',
                  maxWidth: 760,
                  margin: '0 auto 24px',
                  lineHeight: 1.9,
                  position: 'relative',
                  zIndex: 2
                }}
              >
                Access the university portal and discover academic services, student support,
                campus resources, and a modern digital experience designed for everyone.
              </p>

              <button
                className="primary-btn"
                onClick={() => navigate('/login')}
                style={{
                  padding: '14px 24px',
                  borderRadius: 12,
                  border: 'none',
                  background: '#e0c06a',
                  color: '#1a1f5e',
                  fontWeight: 700,
                  fontSize: 15,
                  cursor: 'pointer',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 8,
                  position: 'relative',
                  zIndex: 2
                }}
              >
                Go to Login
                <ArrowRight size={18} />
              </button>
            </div>
          </Reveal>
        </section>
      </div>
    </>
  );
};

export default LandingPage;