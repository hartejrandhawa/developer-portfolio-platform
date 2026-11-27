import { useEffect, useState } from "react";
import { api } from "../api/client";
import { plural } from "../lib/format";

export default function ImportReposPanel({ onImported, onClose }) {
  const [repos, setRepos] = useState(null);
  const [selected, setSelected] = useState(() => new Set());
  const [error, setError] = useState(null);
  const [importing, setImporting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    api.getGitHubRepos()
      .then((data) => !cancelled && setRepos(data))
      .catch((err) => !cancelled && setError(err.message));
    return () => { cancelled = true; };
  }, []);

  function toggle(name) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });
  }

  async function handleImport() {
    setImporting(true);
    setError(null);
    try {
      const created = await api.importRepos([...selected]);
      onImported(created);
    } catch (err) {
      setError(err.message);
      setImporting(false);
    }
  }

  const available = repos?.filter((r) => !r.alreadyImported) ?? [];

  return (
    <section className="import" aria-labelledby="import-heading">
      <div className="import-head">
        <h3 id="import-heading">Add from your GitHub repositories</h3>
        <button type="button" className="btn btn--quiet" onClick={onClose}>Close</button>
      </div>

      {error && <p className="form-error" role="alert">{error}</p>}
      {!repos && !error && <p className="muted">Loading your public repositories…</p>}
      {repos && repos.length === 0 && (
        <p className="muted">No public repositories found on your GitHub account. Add a project by hand instead.</p>
      )}

      {repos && repos.length > 0 && (
        <>
          <ul className="repo-list">
            {repos.map((repo) => (
              <li key={repo.name}>
                <label className={`repo${repo.alreadyImported ? " repo--done" : ""}`}>
                  <input
                    type="checkbox"
                    checked={repo.alreadyImported || selected.has(repo.name)}
                    disabled={repo.alreadyImported}
                    onChange={() => toggle(repo.name)}
                  />
                  <span className="repo-body">
                    <span className="repo-name">
                      {repo.name}
                      {repo.archived && <span className="tag">Archived</span>}
                      {repo.alreadyImported && <span className="tag">On your page</span>}
                    </span>
                    {repo.description && <span className="repo-desc">{repo.description}</span>}
                    <span className="repo-meta">
                      {[repo.language, repo.stars > 0 && plural(repo.stars, "star")].filter(Boolean).join(", ")}
                    </span>
                  </span>
                </label>
              </li>
            ))}
          </ul>
          <div className="actions">
            <button type="button" className="btn btn--primary" onClick={handleImport}
              disabled={selected.size === 0 || importing}>
              {importing ? "Adding…" : selected.size === 0 ? "Select repositories" : `Add ${plural(selected.size, "project")}`}
            </button>
            {available.length > 0 && selected.size < available.length && (
              <button type="button" className="btn btn--quiet"
                onClick={() => setSelected(new Set(available.map((r) => r.name)))}>
                Select all {available.length}
              </button>
            )}
          </div>
        </>
      )}
    </section>
  );
}
