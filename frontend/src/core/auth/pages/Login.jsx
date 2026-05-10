import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Mail,
  Lock,
  Eye,
  EyeOff,
  ArrowRight
} from 'lucide-react';
import authService from '../../api/authService';

const Login = () => {
  const navigate = useNavigate();
  const [showPassword, setShowPassword] = useState(false);
  const [formData, setFormData] = useState({
    nationalId: '',
    password: '',
    remember: false
  });
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value
    }));
    if (error) setError('');
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setIsLoading(true);
    setError('');

    try {
      // Attempt login
      const response = await authService.login({
        nationalId: formData.nationalId,
        password: formData.password
      });

      // Redirect based on profile status
      if (response.status) {
        if (response.status.requiresPasswordChange) {
          navigate('/change-password');
        } else if (response.status.requiresProfileCompletion) {
          navigate('/complete-profile');
        } else {
          navigate('/dashboard'); 
        }
      } else {
        navigate('/dashboard');
      }
    } catch (err) {
      setError(err.message || 'Login failed. Check your national ID and password');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="login-page">
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Space+Mono:wght@400;700&family=DM+Sans:wght@400;500;600;700&display=swap');

        * {
          margin: 0;
          padding: 0;
          box-sizing: border-box;
        }

        body {
          font-family: 'DM Sans', sans-serif;
        }

        .login-page {
          min-height: 100vh;
          background: linear-gradient(135deg, #f4f5f7 0%, #edeef5 100%);
          display: flex;
          align-items: center;
          justify-content: center;
          padding: 20px;
          position: relative;
          overflow: hidden;
        }

        /* ═══ Background Shapes ═══ */
        .bg-shape {
          position: absolute;
          border-radius: 50%;
          opacity: 0.08;
          animation: float 20s ease-in-out infinite;
        }

        .shape-1 {
          width: 300px;
          height: 300px;
          background: #1a1f5e;
          top: -150px;
          left: -150px;
        }

        .shape-2 {
          width: 250px;
          height: 250px;
          background: #c9a84c;
          bottom: -100px;
          right: -100px;
          animation-delay: -5s;
        }

        @keyframes float {
          0%, 100% { transform: translate(0, 0) rotate(0deg); }
          25% { transform: translate(20px, -20px) rotate(90deg); }
          50% { transform: translate(-20px, 20px) rotate(180deg); }
          75% { transform: translate(20px, 20px) rotate(270deg); }
        }

        @keyframes slideUp {
          from { transform: translateY(20px); opacity: 0; }
          to { transform: translateY(0); opacity: 1; }
        }

        @keyframes scaleIn {
          from { transform: scale(0.95); opacity: 0; }
          to { transform: scale(1); opacity: 1; }
        }

        @keyframes pulse {
          0%, 100% { transform: scale(1); }
          50% { transform: scale(1.03); }
        }

        /* ═══ Main Container ═══ */
        .login-container {
          max-width: 900px;
          width: 100%;
          display: grid;
          grid-template-columns: 0.9fr 1fr;
          background: white;
          border-radius: 25px;
          overflow: hidden;
          box-shadow: 0 20px 60px rgba(26, 31, 94, 0.12);
          position: relative;
          z-index: 10;
          animation: scaleIn 0.6s ease;
        }

        /* ═══ Left Panel - Logo ═══ */
        .left-panel {
          background: linear-gradient(135deg, #1a1f5e 0%, #252b7a 50%, #2e3591 100%);
          padding: 50px 35px;
          display: flex;
          flex-direction: column;
          justify-content: center;
          align-items: center;
          position: relative;
          overflow: hidden;
        }

        .left-panel::before {
          content: '';
          position: absolute;
          top: 0;
          left: 0;
          width: 100%;
          height: 100%;
          background-image: url('data:image/svg+xml,%3Csvg width="60" height="60" xmlns="http://www.w3.org/2000/svg"%3E%3Cdefs%3E%3Cpattern id="dots" width="60" height="60" patternUnits="userSpaceOnUse"%3E%3Ccircle cx="2" cy="2" r="1.5" fill="rgba(255,255,255,0.04)"/%3E%3C/pattern%3E%3C/defs%3E%3Crect width="60" height="60" fill="url(%23dots)" /%3E%3C/svg%3E');
          opacity: 0.5;
        }

        .logo-container {
          position: relative;
          z-index: 10;
          text-align: center;
          animation: slideUp 0.8s ease 0.2s both;
        }

        .university-logo {
          width: 100%;
          max-width: 280px;
          animation: pulse 3s ease-in-out infinite;
          filter: drop-shadow(0 8px 20px rgba(0, 0, 0, 0.25));
        }

        /* ═══ Right Panel - Form ═══ */
        .right-panel {
          padding: 50px 45px;
          display: flex;
          flex-direction: column;
          justify-content: center;
          animation: slideUp 0.8s ease 0.3s both;
        }

        .form-header {
          text-align: center;
          margin-bottom: 30px;
        }

        .welcome-text {
          font-size: 26px;
          font-weight: 800;
          color: #1a1f5e;
          margin-bottom: 8px;
          font-family: 'Space Mono', monospace;
        }

        .gold-line {
          width: 50px;
          height: 3px;
          background: linear-gradient(90deg, #c9a84c, #e0c06a);
          border-radius: 2px;
          margin: 0 auto 12px;
        }

        .form-title {
          font-size: 17px;
          font-weight: 600;
          color: #6b7280;
          margin-bottom: 4px;
        }

        .form-subtitle {
          font-size: 13px;
          color: #9ca3af;
        }

        /* Error message */
        .error-message {
          background: rgba(220, 38, 38, 0.1);
          color: #dc2626;
          padding: 12px 16px;
          border-radius: 8px;
          font-size: 13px;
          font-weight: 600;
          margin-bottom: 20px;
          text-align: center;
          border: 1px solid rgba(220, 38, 38, 0.2);
        }

        /* ═══ Form ═══ */
        .login-form {
          display: flex;
          flex-direction: column;
          gap: 20px;
        }

        .input-group {
          position: relative;
        }

        .input-label {
          display: block;
          font-size: 13px;
          font-weight: 600;
          color: #1a1f5e;
          margin-bottom: 8px;
        }

        .input-wrapper {
          position: relative;
        }

        .form-input {
          width: 100%;
          padding: 13px 18px 13px 45px;
          border: 2px solid #e5e7eb;
          border-radius: 10px;
          font-size: 14px;
          transition: all 0.3s;
          background: #f0f1f8;
          color: #1a1f5e;
          font-family: 'DM Sans', sans-serif;
        }

        .form-input:focus {
          outline: none;
          border-color: #c9a84c;
          background: white;
          box-shadow: 0 0 0 3px rgba(201, 168, 76, 0.1);
        }

        .form-input.error {
          border-color: #dc2626;
          background: #fef2f2;
        }

        .input-icon {
          position: absolute;
          left: 15px;
          top: 50%;
          transform: translateY(-50%);
          color: #6b7280;
          transition: all 0.3s;
        }

        .form-input:focus ~ .input-icon {
          color: #c9a84c;
        }

        .password-toggle {
          position: absolute;
          right: 15px;
          top: 50%;
          transform: translateY(-50%);
          background: none;
          border: none;
          color: #6b7280;
          cursor: pointer;
          padding: 4px;
          transition: all 0.3s;
        }

        .password-toggle:hover {
          color: #c9a84c;
        }

        .form-options {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-top: -8px;
        }

        .checkbox-wrapper {
          display: flex;
          align-items: center;
          gap: 7px;
        }

        .checkbox-input {
          width: 16px;
          height: 16px;
          cursor: pointer;
          accent-color: #c9a84c;
        }

        .checkbox-label {
          font-size: 13px;
          color: #6b7280;
          cursor: pointer;
        }

        .forgot-link {
          font-size: 13px;
          color: #c9a84c;
          text-decoration: none;
          font-weight: 600;
          transition: all 0.3s;
        }

        .forgot-link:hover {
          color: #a07828;
          text-decoration: underline;
        }

        .submit-btn {
          width: 100%;
          padding: 14px;
          background: linear-gradient(135deg, #c9a84c, #e0c06a);
          color: #1a1f5e;
          border: none;
          border-radius: 10px;
          font-size: 15px;
          font-weight: 700;
          cursor: pointer;
          transition: all 0.3s;
          display: flex;
          align-items: center;
          justify-content: center;
          gap: 8px;
          box-shadow: 0 5px 18px rgba(201, 168, 76, 0.3);
          font-family: 'DM Sans', sans-serif;
          margin-top: 8px;
        }

        .submit-btn:hover:not(:disabled) {
          transform: translateY(-2px);
          box-shadow: 0 8px 25px rgba(201, 168, 76, 0.4);
        }

        .submit-btn:disabled {
          opacity: 0.7;
          cursor: not-allowed;
        }

        .spinner {
          width: 18px;
          height: 18px;
          border: 3px solid rgba(26, 31, 94, 0.2);
          border-top-color: #1a1f5e;
          border-radius: 50%;
          animation: spin 0.8s linear infinite;
        }

        @keyframes spin {
          to { transform: rotate(360deg); }
        }

        .signup-text {
          text-align: center;
          margin-top: 20px;
          font-size: 13px;
          color: #6b7280;
        }

        .signup-link {
          color: #c9a84c;
          text-decoration: none;
          font-weight: 700;
          transition: all 0.3s;
        }

        .signup-link:hover {
          color: #a07828;
          text-decoration: underline;
        }

        /* ═══ Responsive ═══ */
        @media (max-width: 1024px) {
          .login-container {
            grid-template-columns: 1fr;
            max-width: 450px;
          }

          .left-panel {
            display: none;
          }

          .right-panel {
            padding: 45px 35px;
          }
        }

        @media (max-width: 640px) {
          .right-panel {
            padding: 35px 25px;
          }

          .welcome-text {
            font-size: 22px;
          }

          .form-options {
            flex-direction: column;
            align-items: flex-start;
            gap: 12px;
          }
        }
      `}</style>

      {/* Background Shapes */}
      <div className="bg-shape shape-1" />
      <div className="bg-shape shape-2" />

      {/* Main Container */}
      <div className="login-container">
        {/* Left Panel - University Logo Only */}
        <div className="left-panel">
          <div className="logo-container">
            {/* University Logo - Same as yours */}
            <svg className="university-logo" viewBox="0 0 400 500" xmlns="http://www.w3.org/2000/svg">
              {/* Outer Arc */}
              <path d="M 100 450 Q 100 100 200 100 Q 300 100 300 450" 
                    fill="none" 
                    stroke="#ffffff" 
                    strokeWidth="18" 
                    strokeLinecap="round"
                    opacity="0.9"/>
              
              {/* Atom Symbol - Center Circle */}
              <circle cx="200" cy="250" r="22" fill="url(#goldGradient)"/>
              
              {/* Atom Orbits */}
              <ellipse cx="200" cy="250" rx="75" ry="38" 
                       fill="none" 
                       stroke="#e0c06a" 
                       strokeWidth="2.5"
                       transform="rotate(0 200 250)"/>
              <ellipse cx="200" cy="250" rx="75" ry="38" 
                       fill="none" 
                       stroke="#e0c06a" 
                       strokeWidth="2.5"
                       transform="rotate(60 200 250)"/>
              <ellipse cx="200" cy="250" rx="75" ry="38" 
                       fill="none" 
                       stroke="#e0c06a" 
                       strokeWidth="2.5"
                       transform="rotate(120 200 250)"/>
              
              {/* Atom Electrons */}
              <circle cx="275" cy="250" r="7" fill="#e0c06a"/>
              <circle cx="125" cy="250" r="7" fill="#e0c06a"/>
              <circle cx="238" cy="218" r="7" fill="#e0c06a"/>
              <circle cx="162" cy="282" r="7" fill="#e0c06a"/>
              <circle cx="238" cy="282" r="7" fill="#e0c06a"/>
              <circle cx="162" cy="218" r="7" fill="#e0c06a"/>
              
              {/* Arabic Text - University */}
              <text x="200" y="360" 
                    fontFamily="Arial, sans-serif" 
                    fontSize="38" 
                    fontWeight="bold" 
                    fill="#ffffff" 
                    textAnchor="middle"
                    opacity="0.95">
                جامعة
              </text>
              
              {/* Arabic Text - Capital */}
              <text x="200" y="408" 
                    fontFamily="Arial, sans-serif" 
                    fontSize="45" 
                    fontWeight="bold" 
                    fill="#ffffff" 
                    textAnchor="middle"
                    opacity="0.95">
                العاصمة
              </text>
              
              {/* English Text - CAPITAL */}
              <text x="200" y="448" 
                    fontFamily="Arial, sans-serif" 
                    fontSize="30" 
                    fontWeight="bold" 
                    fill="#ffffff" 
                    textAnchor="middle" 
                    letterSpacing="2"
                    opacity="0.95">
                CAPITAL
              </text>
              
              {/* English Text - UNIVERSITY */}
              <text x="200" y="478" 
                    fontFamily="Arial, sans-serif" 
                    fontSize="22" 
                    fontWeight="bold" 
                    fill="#ffffff" 
                    textAnchor="middle" 
                    letterSpacing="3"
                    opacity="0.95">
                UNIVERSITY
              </text>
              
              {/* Gold Gradient Definition */}
              <defs>
                <linearGradient id="goldGradient" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" style={{ stopColor: '#f5e5a0', stopOpacity: 1 }} />
                  <stop offset="100%" style={{ stopColor: '#e0c06a', stopOpacity: 1 }} />
                </linearGradient>
              </defs>
            </svg>
          </div>
        </div>

        {/* Right Panel - Login Form */}
        <div className="right-panel">
          <div className="form-header">
            <h1 className="welcome-text">Welcome Back</h1>
            <div className="gold-line" />
            <h2 className="form-title">Admin Login</h2>
            <p className="form-subtitle">Enter your credentials to access the dashboard</p>
          </div>

          {/* Error message */}
          {error && (
            <div className="error-message">
              {error}
            </div>
          )}

          <form className="login-form" onSubmit={handleSubmit}>
            {/* National ID */}
            <div className="input-group">
              <label className="input-label">National ID</label>
              <div className="input-wrapper">
                <input
                  type="text"
                  name="nationalId"
                  className={`form-input ${error ? 'error' : ''}`}
                  placeholder="Enter your national ID"
                  value={formData.nationalId}
                  onChange={handleChange}
                  required
                  maxLength="14"
                />
                <Mail size={18} className="input-icon" />
              </div>
            </div>

            {/* Password */}
            <div className="input-group">
              <label className="input-label">Password</label>
              <div className="input-wrapper">
                <input
                  type={showPassword ? 'text' : 'password'}
                  name="password"
                  className={`form-input ${error ? 'error' : ''}`}
                  placeholder="Enter your password"
                  value={formData.password}
                  onChange={handleChange}
                  required
                />
                <Lock size={18} className="input-icon" />
                <button
                  type="button"
                  className="password-toggle"
                  onClick={() => setShowPassword(!showPassword)}
                >
                  {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </div>
            </div>

            {/* Options */}
            <div className="form-options">
              <div className="checkbox-wrapper">
                <input
                  type="checkbox"
                  name="remember"
                  id="remember"
                  className="checkbox-input"
                  checked={formData.remember}
                  onChange={handleChange}
                />
                <label htmlFor="remember" className="checkbox-label">
                  Remember me
                </label>
              </div>
              <a href="#" className="forgot-link">Forgot password?</a>
            </div>

            {/* Submit button */}
            <button type="submit" className="submit-btn" disabled={isLoading}>
              {isLoading ? (
                <>
                  <div className="spinner" />
                  Logging in...
                </>
              ) : (
                <>
                  Login
                  <ArrowRight size={18} />
                </>
              )}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
};

export default Login;