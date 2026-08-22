import type { ReactNode } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { getSession } from './api/client';
import { LoginPage } from './pages/LoginPage';
import { QuotePage } from './pages/QuotePage';
import './App.css';

function RequireAuth({ children }: Readonly<{ children: ReactNode }>) {
  const { accessToken } = getSession();
  if (!accessToken) {
    return <Navigate to="/" replace />;
  }
  return children;
}

function App() {
  return (
    <div className="app-shell">
      <header className="topbar">
        <span className="brand">Aspire Quotes</span>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<LoginPage />} />
          <Route
            path="/quote"
            element={
              <RequireAuth>
                <QuotePage />
              </RequireAuth>
            }
          />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;
