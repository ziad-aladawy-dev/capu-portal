import React from "react";
import "./HorizontalNav.css";

export const HorizontalNav = ({ items = [] }) => {
  if (!items || items.length === 0) return null;

  return (
    <nav className="horizontal-nav">
      <div className="horizontal-nav-container">
        {items.map((item, index) => (
          <a
            key={`${item.path}-${index}`}
            href={item.path}
            className="horizontal-nav-item"
          >
            {item.label}
          </a>
        ))}
      </div>
    </nav>
  );
};
