import React from 'react';

const LoadingSpinner = ({ fullPage = false, message = "Loading..." }) => {
  const style = {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    ...(fullPage ? { minHeight: '100vh' } : { padding: '40px' }),
    gap: '16px'
  };

  const spinnerStyle = {
    width: fullPage ? '50px' : '40px',
    height: fullPage ? '50px' : '40px',
    border: '4px solid #e5e7eb',
    borderTop: '4px solid var(--navy-primary)',
    borderRadius: '50%',
    animation: 'spin 1s linear infinite'
  };

  return (
    <div style={style}>
      <style>{`
        @keyframes spin {
          to { transform: rotate(360deg); }
        }
      `}</style>
      <div style={spinnerStyle}></div>
      <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>{message}</p>
    </div>
  );
};

export default LoadingSpinner;