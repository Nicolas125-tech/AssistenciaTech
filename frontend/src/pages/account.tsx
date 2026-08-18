import { AccountView } from '@neondatabase/neon-js/auth/react';
import { useNavigate } from 'react-router-dom';

export function Account() {
  const navigate = useNavigate();

  return (
    <div style={{ 
      minHeight: '100vh', 
      backgroundColor: '#0d1117',
      color: '#c9d1d9',
      padding: '2rem 1rem',
      fontFamily: 'system-ui, -apple-system, sans-serif'
    }}>
      <div style={{
        maxWidth: '800px',
        margin: '0 auto',
        backgroundColor: '#161b22',
        padding: '2rem',
        borderRadius: '12px',
        border: '1px solid #30363d',
        boxShadow: '0 8px 24px rgba(0,0,0,0.3)'
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem', borderBottom: '1px solid #30363d', paddingBottom: '1rem' }}>
          <h2 style={{ margin: 0, color: '#58a6ff' }}>Gerenciamento de Conta</h2>
          <button 
            onClick={() => navigate('/')} 
            style={{ 
              padding: '0.5rem 1rem', 
              backgroundColor: '#21262d', 
              border: '1px solid #30363d', 
              borderRadius: '6px', 
              color: '#c9d1d9', 
              cursor: 'pointer',
              fontWeight: 'bold'
            }}
          >
            Voltar ao Início
          </button>
        </div>
        <AccountView />
      </div>
    </div>
  );
}
