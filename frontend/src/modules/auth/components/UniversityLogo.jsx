function UniversityLogo() {
  return (
    <div className="logo-container">
      <svg
        className="university-logo"
        viewBox="0 0 400 500"
        xmlns="http://www.w3.org/2000/svg"
      >
        <path
          d="M 100 450 Q 100 100 200 100 Q 300 100 300 450"
          fill="none"
          stroke="#ffffff"
          strokeWidth="18"
          strokeLinecap="round"
          opacity="0.9"
        />

        <circle cx="200" cy="250" r="22" fill="url(#goldGradient)" />

        <ellipse
          cx="200"
          cy="250"
          rx="75"
          ry="38"
          fill="none"
          stroke="#e0c06a"
          strokeWidth="2.5"
        />

        <ellipse
          cx="200"
          cy="250"
          rx="75"
          ry="38"
          fill="none"
          stroke="#e0c06a"
          strokeWidth="2.5"
          transform="rotate(60 200 250)"
        />

        <ellipse
          cx="200"
          cy="250"
          rx="75"
          ry="38"
          fill="none"
          stroke="#e0c06a"
          strokeWidth="2.5"
          transform="rotate(120 200 250)"
        />

        <circle cx="275" cy="250" r="7" fill="#e0c06a" />
        <circle cx="125" cy="250" r="7" fill="#e0c06a" />
        <circle cx="238" cy="218" r="7" fill="#e0c06a" />
        <circle cx="162" cy="282" r="7" fill="#e0c06a" />
        <circle cx="238" cy="282" r="7" fill="#e0c06a" />
        <circle cx="162" cy="218" r="7" fill="#e0c06a" />

        <text
          x="200"
          y="360"
          fontFamily="Arial, sans-serif"
          fontSize="38"
          fontWeight="bold"
          fill="#ffffff"
          textAnchor="middle"
        >
          جامعة
        </text>

        <text
          x="200"
          y="408"
          fontFamily="Arial, sans-serif"
          fontSize="45"
          fontWeight="bold"
          fill="#ffffff"
          textAnchor="middle"
        >
          العاصمة
        </text>

        <text
          x="200"
          y="448"
          fontFamily="Arial, sans-serif"
          fontSize="30"
          fontWeight="bold"
          fill="#ffffff"
          textAnchor="middle"
          letterSpacing="2"
        >
          CAPITAL
        </text>

        <text
          x="200"
          y="478"
          fontFamily="Arial, sans-serif"
          fontSize="22"
          fontWeight="bold"
          fill="#ffffff"
          textAnchor="middle"
          letterSpacing="3"
        >
          UNIVERSITY
        </text>

        <defs>
          <linearGradient id="goldGradient" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#f5e5a0" />
            <stop offset="100%" stopColor="#e0c06a" />
          </linearGradient>
        </defs>
      </svg>
    </div>
  );
}

export default UniversityLogo;