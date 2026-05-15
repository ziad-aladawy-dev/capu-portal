import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  ArrowRight,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";

import { slides } from "../data/landingData";

function HeroSlider() {
  const navigate = useNavigate();

  const [currentSlide, setCurrentSlide] = useState(0);

  const nextSlide = () => {
    setCurrentSlide((prev) => (prev + 1) % slides.length);
  };

  const prevSlide = () => {
    setCurrentSlide((prev) =>
      (prev - 1 + slides.length) % slides.length
    );
  };

  useEffect(() => {
    const interval = setInterval(() => {
      nextSlide();
    }, 4500);

    return () => clearInterval(interval);
  }, []);

  const activeSlide = slides[currentSlide];

  return (
    <section className="hero-section">
      <div className="hero-slider">

        {slides.map((slide, index) => (
          <div
            key={index}
            className={`hero-slide-fade ${
              index === currentSlide ? "active" : ""
            }`}
          >
            <img src={slide.image} alt={slide.title} />

            <div className="hero-overlay" />
          </div>
        ))}

        <div className="floating-shape pulse-shape shape-one" />

        <div className="floating-shape delay-2 shape-two" />

        <div className="floating-shape delay-3 pulse-shape shape-three" />

        <div className="hero-content-wrapper">
          <div className="hero-content" key={currentSlide}>
            <p>{activeSlide.subtitle}</p>

            <h1>{activeSlide.title}</h1>

            <span>{activeSlide.description}</span>

            <div className="hero-buttons">
              <button
                className="primary-btn"
                onClick={() => navigate("/admin/login")}
              >
                {activeSlide.buttonText}

                <ArrowRight size={18} />
              </button>

              <button
                className="secondary-btn"
                onClick={() => navigate("/admin/login")}
              >
                Login to Portal
              </button>
            </div>
          </div>
        </div>

        <button
          className="slider-arrow left"
          onClick={prevSlide}
        >
          <ChevronLeft size={22} />
        </button>

        <button
          className="slider-arrow right"
          onClick={nextSlide}
        >
          <ChevronRight size={22} />
        </button>

        <div className="slider-dots">
          {slides.map((_, index) => (
            <button
              key={index}
              onClick={() => setCurrentSlide(index)}
              className={
                index === currentSlide
                  ? "dot active"
                  : "dot"
              }
            />
          ))}
        </div>
      </div>
    </section>
  );
}

export default HeroSlider;