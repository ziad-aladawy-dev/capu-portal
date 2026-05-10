import React from "react";
import { Menu, Bell, Moon } from "lucide-react";
import "./Navbar.css";

const Navbar = ({ onMenuClick }) => {
  return (
    <header className="navbar">
      <div className="navbar-left">
        <button className="navbar-icon-btn" onClick={onMenuClick}>
          <Menu size={22} />
        </button>
      </div>

      <div className="navbar-right">
        <button className="navbar-icon-btn">
          <Moon size={18} />
        </button>

        <button className="navbar-icon-btn">
          <Bell size={18} />
        </button>
      </div>

    </header>
  );
};

export default Navbar;
