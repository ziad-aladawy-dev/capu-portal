import PropTypes from "prop-types";
import { useEffect, useRef, useState } from "react";

function Reveal({ children }) {
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
      className={visible ? "reveal show" : "reveal"}
    >
      {children}
    </div>
  );
}

export default Reveal;

Reveal.propTypes = {
  children: PropTypes.node,
};