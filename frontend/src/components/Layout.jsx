import { Link, NavLink, Outlet } from "react-router-dom";
import { api } from "../api/client";
import { useAuthToken } from "../lib/auth";

export function Wordmark() {
  return (
    <Link to="/" className="wordmark" aria-label="Dev Portfolio home">
      <svg viewBox="0 0 16 16" width="18" height="18" aria-hidden="true">
        <rect x="0" y="0" width="7" height="7" rx="1.5" className="wm-1" />
        <rect x="9" y="0" width="7" height="7" rx="1.5" className="wm-2" />
        <rect x="0" y="9" width="7" height="7" rx="1.5" className="wm-3" />
        <rect x="9" y="9" width="7" height="7" rx="1.5" className="wm-4" />
      </svg>
      <span>Dev Portfolio</span>
    </Link>
  );
}

export default function Layout() {
  const token = useAuthToken();
  return (
    <div className="shell">
      <a className="skip-link" href="#main">Skip to content</a>
      <header className="topbar">
        <Wordmark />
        <nav className="topbar-nav">
          {token ? (
            <NavLink to="/dashboard" className="nav-link">Your dashboard</NavLink>
          ) : (
            <a href={api.loginUrl()} className="nav-link">Log in with GitHub</a>
          )}
        </nav>
      </header>
      <main id="main" className="main">
        <Outlet />
      </main>
      <footer className="footer">
        <p>Built from public GitHub data. Your projects, your order, your link.</p>
      </footer>
    </div>
  );
}
