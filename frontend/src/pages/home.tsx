import { neon } from '../lib/neon';
import { useNavigate } from 'react-router-dom';
import { useEffect } from 'react';

export function Home() {
  const { data: session, isPending } = neon.auth.useSession();
  const navigate = useNavigate();

  useEffect(() => {
    if (!isPending && !session) {
      navigate('/auth/sign-in');
    }
  }, [session, isPending, navigate]);

  if (isPending) {
    return (
      <div style={{ 
        display: 'flex', 
        justifyContent: 'center', 
        alignItems: 'center', 
        height: '100vh', 
        flexDirection: 'column', 
        gap: '1rem', 
        backgroundColor: '#0d1117', 
        color: '#c9d1d9',
        fontFamily: 'system-ui, -apple-system, sans-serif'
      }}>
        <div style={{ 
          border: '4px solid #30363d', 
          borderTop: '4px solid #58a6ff', 
          borderRadius: '50%', 
          width: '40px', 
          height: '40px', 
          animation: 'spin 1s linear infinite' 
        }}></div>
        <p>Carregando sessão...</p>
        <style>{`
          @keyframes spin {
            0% { transform: rotate(0deg); }
            100% { transform: rotate(360deg); }
          }
        `}</style>
      </div>
    );
  }

  if (!session) return null;

  return (
    <div style={{ 
      minHeight: '100vh',
      backgroundColor: '#0d1117',
      color: '#c9d1d9',
      padding: '4rem 1rem',
      fontFamily: 'system-ui, -apple-system, sans-serif'
    }}>
      <div style={{ 
        maxWidth: '800px', 
        margin: '0 auto', 
        padding: '2.5rem', 
        backgroundColor: '#161b22', 
        borderRadius: '12px', 
        border: '1px solid #30363d', 
        boxShadow: '0 8px 24px rgba(0,0,0,0.3)'
      }}>
        <h1 style={{ borderBottom: '1px solid #30363d', paddingBottom: '1rem', color: '#58a6ff', marginTop: 0 }}>
          Área do Cliente (TechOS)
        </h1>
        <p style={{ fontSize: '1.2rem', marginTop: '1.5rem' }}>
          Olá, <strong>{session.user.name || session.user.email}</strong>!
        </p>
        
        <div style={{ margin: '2rem 0', padding: '1.5rem', backgroundColor: '#0d1117', borderRadius: '8px', border: '1px solid #30363d' }}>
          <h3 style={{ marginTop: 0, color: '#c9d1d9' }}>Informações da Conta</h3>
          <p><strong>Nome:</strong> {session.user.name}</p>
          <p><strong>E-mail:</strong> {session.user.email}</p>
          <p><strong>ID do Usuário:</strong> {session.user.id}</p>
        </div>

        <div style={{ display: 'flex', gap: '1rem' }}>
          <button 
            onClick={() => navigate('/account/sessions')} 
            style={{ 
              padding: '0.75rem 1.5rem', 
              backgroundColor: '#21262d', 
              border: '1px solid #30363d', 
              borderRadius: '6px', 
              color: '#c9d1d9', 
              cursor: 'pointer', 
              fontWeight: 'bold',
              transition: 'background-color 0.2s'
            }}
            onMouseOver={(e) => e.currentTarget.style.backgroundColor = '#30363d'}
            onMouseOut={(e) => e.currentTarget.style.backgroundColor = '#21262d'}
          >
            Gerenciar Conta
          </button>
          <button 
            onClick={async () => {
              await neon.auth.signOut();
              navigate('/auth/sign-in');
            }} 
            style={{ 
              padding: '0.75rem 1.5rem', 
              backgroundColor: '#da3637', 
              border: 'none', 
              borderRadius: '6px', 
              color: '#ffffff', 
              cursor: 'pointer', 
              fontWeight: 'bold',
              transition: 'background-color 0.2s'
            }}
            onMouseOver={(e) => e.currentTarget.style.backgroundColor = '#f85149'}
            onMouseOut={(e) => e.currentTarget.style.backgroundColor = '#da3637'}
          >
            Sair da Conta
          </button>
        </div>
      </div>
    </div>
  );
}
