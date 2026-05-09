import React from 'react';
import { AlertCircle, RefreshCw } from 'lucide-react';

const ErrorMessage = ({ message, onRetry }) => {
  const style = {
    textAlign: 'center',
    padding: '40px 20px',
    background: 'var(--pure-white)',
    borderRadius: '20px',
    margin: '20px 0',
    boxShadow: '0 8px 30px rgba(26, 31, 94, 0.08)',
    border: '1px solid var(--border-color)'
  };

  return (
    <div style={style}>
      <AlertCircle size={48} color="var(--warning-red)" style={{ marginBottom: '16px' }} />
      <h2 style={{ color: 'var(--warning-red)', marginBottom: '12px', fontSize: '20px' }}>
        Error Loading Data
      </h2>
      <p style={{ marginBottom: '24px', color: 'var(--text-muted)' }}>{message}</p>
      {onRetry && (
        <button
          onClick={onRetry}
          style={{
            padding: '12px 24px',
            background: 'linear-gradient(135deg, var(--navy-primary), var(--navy-accent))',
            color: 'white',
            border: 'none',
            borderRadius: '10px',
            cursor: 'pointer',
            display: 'inline-flex',
            alignItems: 'center',
            gap: '8px',
            fontSize: '14px',
            fontWeight: '600',
            transition: 'all 0.3s'
          }}
          onMouseEnter={(e) => e.target.style.transform = 'translateY(-2px)'}
          onMouseLeave={(e) => e.target.style.transform = 'translateY(0)'}
        >
          <RefreshCw size={16} /> Try Again
        </button>
      )}
    </div>
  );
};

export default ErrorMessage;