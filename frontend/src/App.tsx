import type { ReactNode } from 'react';
import { Navigate, NavLink, Outlet, Route, Routes } from 'react-router-dom';
import { getSession } from './api/client';
import { LoginPage } from './pages/LoginPage';
import { PublishQuotePage } from './pages/PublishQuotePage';
import { QuotePage } from './pages/QuotePage';
import { QuotesListPage } from './pages/QuotesListPage';
import './App.css';

function RequireAuth({ children }: Readonly<{ children: ReactNode }>) {
  const { accessToken } = getSession();
  if (!accessToken) {
    return <Navigate to="/" replace />;
  }
  return children;
}

/** The shared shell of every signed-in page: section nav above the routed page. */
function AuthLayout() {
  return (
    <div className="authenticated">
      <nav className="main-nav" aria-label="Sections">
        <NavLink to="/quote">Random</NavLink>
        <NavLink to="/quotes">Browse</NavLink>
        <NavLink to="/publish">Publish</NavLink>
      </nav>
      <Outlet />
    </div>
  );
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
            element={
              <RequireAuth>
                <AuthLayout />
              </RequireAuth>
            }
          >
            <Route path="/quote" element={<QuotePage />} />
            <Route path="/quotes" element={<QuotesListPage />} />
            <Route path="/publish" element={<PublishQuotePage />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
      <footer className="app-footer">
        <span>A .NET Aspire proof of concept</span>
        <span>Set in Fraunces &amp; IBM Plex</span>
      </footer>
    </div>
  );
}

export default App;
