import { useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { api } from "../api/client";
import { useAuthToken } from "../lib/auth";
import ActivityHeatmap from "../components/ActivityHeatmap";

const ERRORS = {
  access_denied: "GitHub login was cancelled. Log in again whenever you're ready.",
  login_failed: "GitHub login didn't complete. Try again.",
  session_expired: "Your session has ended. Log in again to continue.",
};

// Deterministic sample data for the hero, so it looks the same on every visit.
function sampleActivity() {
  let seed = 7;
  const rand = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
  const now = new Date();
  const dayIso = (offset) =>
    new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() - offset)).toISOString().slice(0, 10);

  const days = [];
  for (let i = 90; i >= 0; i--) {
    const date = dayIso(i);
    const weekday = new Date(`${date}T00:00:00Z`).getUTCDay();
    const weekend = weekday === 0 || weekday === 6;
    const count = rand() < (weekend ? 0.6 : 0.2) ? 0 : Math.ceil(rand() * (weekend ? 3 : 9));
    if (count) days.push({ date, count });
  }
  return { from: dayIso(90), to: dayIso(0), days, total: days.reduce((s, d) => s + d.count, 0) };
}

export default function Home() {
  const [username, setUsername] = useState("");
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const token = useAuthToken();
  const sample = useMemo(sampleActivity, []);
  const error = ERRORS[params.get("error")];

  function handleLookup(e) {
    e.preventDefault();
    const name = username.trim().replace(/^@/, "");
    if (name) navigate(`/u/${encodeURIComponent(name)}`);
  }

  return (
    <div className="home">
      {error && <p className="banner" role="alert">{error}</p>}

      <section className="hero">
        <div className="hero-copy">
          <h1 className="display">Show the work, not just the résumé.</h1>
          <p className="lede">
            Sign in with GitHub, pick the repositories worth showing, and get one link
            with your projects and recent activity on it.
          </p>
          <div className="actions">
            {token ? (
              <Link className="btn btn--primary btn--large" to="/dashboard">Open your dashboard</Link>
            ) : (
              <a className="btn btn--primary btn--large" href={api.loginUrl()}>Log in with GitHub</a>
            )}
          </div>
        </div>
        <div className="hero-art">
          <ActivityHeatmap activity={sample} animate />
          <p className="caption">Thirteen weeks of commits, pull requests and issues, drawn from GitHub.</p>
        </div>
      </section>

      <section className="steps" aria-labelledby="steps-heading">
        <h2 id="steps-heading">How it works</h2>
        <ol>
          <li>
            <h3>Log in with GitHub</h3>
            <p>No new password. We only read your public profile.</p>
          </li>
          <li>
            <h3>Choose your projects</h3>
            <p>Import repositories in one go or write them up by hand, then put them in the order you want.</p>
          </li>
          <li>
            <h3>Share your link</h3>
            <p>Your page lives at a short address anyone can open, no account needed.</p>
          </li>
        </ol>
      </section>

      <section className="lookup" aria-labelledby="lookup-heading">
        <h2 id="lookup-heading">Look someone up</h2>
        <form className="lookup-form" onSubmit={handleLookup}>
          <label htmlFor="lookup" className="visually-hidden">GitHub username</label>
          <span className="lookup-prefix" aria-hidden="true">/u/</span>
          <input id="lookup" placeholder="github-username" value={username}
            onChange={(e) => setUsername(e.target.value)} autoComplete="off" spellCheck={false} />
          <button type="submit" className="btn">View page</button>
        </form>
      </section>
    </div>
  );
}
