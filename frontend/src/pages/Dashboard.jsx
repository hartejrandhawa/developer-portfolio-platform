import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api } from "../api/client";
import { useAuthToken } from "../lib/auth";
import { timeAgo } from "../lib/format";
import ActivityHeatmap from "../components/ActivityHeatmap";
import ImportReposPanel from "../components/ImportReposPanel";
import ProjectForm from "../components/ProjectForm";
import TechChips from "../components/TechChips";

export default function Dashboard() {
  const token = useAuthToken();
  const navigate = useNavigate();
  const [me, setMe] = useState(null);
  const [projects, setProjects] = useState(null);
  const [loadError, setLoadError] = useState(null);
  const [notice, setNotice] = useState(null);

  // A 401 anywhere clears the token; this sends the user home, explaining why
  // only if they actually had a session (not on a cold visit to /dashboard).
  const hadSession = useRef(Boolean(token));
  useEffect(() => {
    if (!token) navigate(hadSession.current ? "/?error=session_expired" : "/", { replace: true });
  }, [token, navigate]);

  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    Promise.all([api.getMe(), api.getMyProjects()])
      .then(([meData, projectData]) => {
        if (cancelled) return;
        setMe(meData);
        setProjects(projectData);
      })
      .catch((err) => !cancelled && setLoadError(err.message));
    return () => { cancelled = true; };
  }, [token]);

  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => setNotice(null), 4000);
    return () => clearTimeout(t);
  }, [notice]);

  if (loadError) return <p className="form-error page-pad" role="alert">{loadError}</p>;
  if (!me || !projects) return <p className="muted page-pad" aria-live="polite">Loading your dashboard…</p>;

  return (
    <div className="dashboard">
      <header className="dash-head">
        <div>
          <h1 className="display display--small">Your page</h1>
          <ShareLink username={me.username} />
        </div>
      </header>

      {notice && <p className="toast" role="status">{notice}</p>}

      <div className="dash-grid">
        <aside className="dash-side">
          <ProfileEditor me={me} onSaved={(updated) => { setMe(updated); setNotice("Profile saved"); }} />
          <ActivityPanel />
        </aside>
        <ProjectsPanel projects={projects} setProjects={setProjects} setNotice={setNotice} />
      </div>
    </div>
  );
}

function ShareLink({ username }) {
  const [copied, setCopied] = useState(false);
  const path = `/u/${username}`;
  const url = `${window.location.origin}${path}`;

  async function copy() {
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  }

  return (
    <div className="share">
      <Link to={path} className="share-url">{url.replace(/^https?:\/\//, "")}</Link>
      <button type="button" className="btn btn--small" onClick={copy} aria-live="polite">
        {copied ? "Copied" : "Copy link"}
      </button>
    </div>
  );
}

function ProfileEditor({ me, onSaved }) {
  const [displayName, setDisplayName] = useState(me.displayName ?? "");
  const [bio, setBio] = useState(me.bio ?? "");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const dirty = displayName !== (me.displayName ?? "") || bio !== (me.bio ?? "");

  async function handleSubmit(e) {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      onSaved(await api.updateProfile({ displayName, bio }));
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <form className="panel" onSubmit={handleSubmit} aria-labelledby="profile-heading">
      <div className="panel-id">
        {me.avatarUrl && <img src={me.avatarUrl} alt="" className="avatar" width="48" height="48" />}
        <div>
          <h2 id="profile-heading" className="panel-title">About you</h2>
          <p className="muted">@{me.username}</p>
        </div>
      </div>
      <div className="field">
        <label htmlFor="displayName">Name</label>
        <input id="displayName" value={displayName} maxLength={100} placeholder={me.username}
          onChange={(e) => setDisplayName(e.target.value)} />
      </div>
      <div className="field">
        <label htmlFor="bio">Bio</label>
        <textarea id="bio" value={bio} maxLength={500} rows={4}
          placeholder="What you build and what you're looking for."
          onChange={(e) => setBio(e.target.value)} aria-describedby="bio-count" />
        <p id="bio-count" className="hint">{500 - bio.length} characters left</p>
      </div>
      {error && <p className="form-error" role="alert">{error}</p>}
      <button type="submit" className="btn btn--primary" disabled={!dirty || saving}>
        {saving ? "Saving…" : "Save profile"}
      </button>
    </form>
  );
}

function ActivityPanel() {
  const [activity, setActivity] = useState(null);
  const [syncing, setSyncing] = useState(true);
  const [error, setError] = useState(null);

  // The server throttles real GitHub calls to one per 10 minutes, so it's safe
  // to ask on every dashboard visit; most calls just return the stored summary.
  const sync = useCallback(async () => {
    setSyncing(true);
    setError(null);
    try {
      setActivity(await api.syncActivity());
    } catch (err) {
      setError(err.message);
    } finally {
      setSyncing(false);
    }
  }, []);

  useEffect(() => { sync(); }, [sync]);

  return (
    <section className="panel" aria-labelledby="activity-panel-heading">
      <h2 id="activity-panel-heading" className="panel-title">GitHub activity</h2>
      {activity && <ActivityHeatmap activity={activity} size="compact" />}
      {activity && (
        <p className="muted">
          {activity.total > 0 ? `${activity.total} contributions in 13 weeks.` : "No public activity in 13 weeks."}{" "}
          {activity.syncedAt && `Updated ${timeAgo(activity.syncedAt)}.`}
        </p>
      )}
      {error && <p className="form-error" role="alert">{error}</p>}
      <button type="button" className="btn btn--small" onClick={sync} disabled={syncing}>
        {syncing ? "Checking GitHub…" : "Refresh activity"}
      </button>
    </section>
  );
}

function ProjectsPanel({ projects, setProjects, setNotice }) {
  // "new" | project id | null: which form, if any, is open
  const [editing, setEditing] = useState(null);
  const [importing, setImporting] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [error, setError] = useState(null);

  async function create(form) {
    const created = await api.createProject(form);
    setProjects((ps) => [...ps, created]);
    setEditing(null);
    setNotice("Project added");
  }

  async function update(id, form) {
    const updated = await api.updateProject(id, form);
    setProjects((ps) => ps.map((p) => (p.id === id ? updated : p)));
    setEditing(null);
    setNotice("Changes saved");
  }

  async function remove(id) {
    setError(null);
    try {
      await api.deleteProject(id);
      setProjects((ps) => ps.filter((p) => p.id !== id));
      setNotice("Project deleted");
    } catch (err) {
      setError(err.message);
    } finally {
      setConfirmDelete(null);
    }
  }

  // Optimistic: move it now, roll back if the server refuses.
  async function move(index, delta) {
    const target = index + delta;
    if (target < 0 || target >= projects.length) return;
    const previous = projects;
    const next = [...projects];
    [next[index], next[target]] = [next[target], next[index]];
    setProjects(next);
    setError(null);
    try {
      await api.reorderProjects(next.map((p) => p.id));
    } catch (err) {
      setProjects(previous);
      setError(err.message);
    }
  }

  return (
    <section className="dash-main" aria-labelledby="projects-heading">
      <div className="section-head">
        <h2 id="projects-heading">Projects</h2>
        <div className="actions">
          <button type="button" className="btn" onClick={() => { setImporting(true); setEditing(null); }}
            disabled={importing}>Import from GitHub</button>
          <button type="button" className="btn btn--primary" onClick={() => { setEditing("new"); setImporting(false); }}
            disabled={editing === "new"}>Add project</button>
        </div>
      </div>

      {error && <p className="form-error" role="alert">{error}</p>}

      {importing && (
        <ImportReposPanel
          onClose={() => setImporting(false)}
          onImported={(created) => {
            setProjects((ps) => [...ps, ...created]);
            setImporting(false);
            setNotice(created.length === 1 ? "1 project added" : `${created.length} projects added`);
          }}
        />
      )}

      {editing === "new" && (
        <div className="panel">
          <h3 className="panel-title">New project</h3>
          <ProjectForm submitLabel="Add project" onSubmit={create} onCancel={() => setEditing(null)} />
        </div>
      )}

      {projects.length === 0 && editing !== "new" && !importing ? (
        <div className="empty">
          <p>Your page has no projects yet. Import your GitHub repositories to start, or add one by hand.</p>
        </div>
      ) : (
        <ol className="manage-list">
          {projects.map((p, i) => (
            <li key={p.id} className="manage-item">
              {editing === p.id ? (
                <ProjectForm initial={p} submitLabel="Save changes"
                  onSubmit={(form) => update(p.id, form)} onCancel={() => setEditing(null)} />
              ) : (
                <>
                  <div className="order-controls">
                    <button type="button" className="icon-btn" onClick={() => move(i, -1)} disabled={i === 0}
                      aria-label={`Move ${p.title} up`}>
                      <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M4 10l4-4 4 4" /></svg>
                    </button>
                    <button type="button" className="icon-btn" onClick={() => move(i, 1)} disabled={i === projects.length - 1}
                      aria-label={`Move ${p.title} down`}>
                      <svg viewBox="0 0 16 16" aria-hidden="true"><path d="M4 6l4 4 4-4" /></svg>
                    </button>
                  </div>
                  <div className="manage-body">
                    <h3>{p.title}</h3>
                    {p.description && <p className="clamp">{p.description}</p>}
                    <TechChips value={p.techStack} />
                  </div>
                  <div className="manage-actions">
                    {confirmDelete === p.id ? (
                      <>
                        <span className="confirm-text">Delete this project?</span>
                        <button type="button" className="btn btn--small btn--danger" onClick={() => remove(p.id)}>Delete</button>
                        <button type="button" className="btn btn--small btn--quiet" onClick={() => setConfirmDelete(null)}>Keep</button>
                      </>
                    ) : (
                      <>
                        <button type="button" className="btn btn--small" onClick={() => { setEditing(p.id); setImporting(false); }}>Edit</button>
                        <button type="button" className="btn btn--small btn--quiet" onClick={() => setConfirmDelete(p.id)}>Delete</button>
                      </>
                    )}
                  </div>
                </>
              )}
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
