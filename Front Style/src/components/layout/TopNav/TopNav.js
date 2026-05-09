import React from 'react';
import { Menu, Moon, Bell } from 'lucide-react';
import './TopNav.css';

const TopNav = ({ onMenuClick }) => (
  <div className="top-nav">
    <div className="nav-left">
      <Menu size={24} className="menu-icon" onClick={onMenuClick}/>
    </div>
    <div className="nav-right">
      <Moon size={20} className="nav-icon" />
      <Bell size={20} className="nav-icon" />
    </div>
  </div>
);

export default TopNav;