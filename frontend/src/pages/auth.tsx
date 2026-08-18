import { AuthView } from '@neondatabase/neon-js/auth/react';

export function Auth() {
  return (
    <div style={{ 
      display: 'flex', 
      justifyContent: 'center', 
      alignItems: 'center', 
      minHeight: '100vh', 
      backgroundColor: '#0d1117',
      padding: '2rem'
    }}>
      <div style={{
        width: '100%',
        maxWidth: '450px',
        backgroundColor: '#161b22',
        padding: '2rem',
        borderRadius: '12px',
        border: '1px solid #30363d',
        boxShadow: '0 8px 24px rgba(0,0,0,0.3)'
      }}>
        <AuthView />
      </div>
    </div>
  );
}
