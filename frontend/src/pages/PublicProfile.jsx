import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api } from "../api/client";
import { activitySentence } from "../lib/activity";
import { safeUrl } from "../lib/format";
import ActivityHeatmap from "../components/ActivityHeatmap";
import TechChips from "../components/TechChips";

export default function PublicProfile() {
  const { username } = useParams();
  const [state, setState] = useState({ status: "loading" });

  useEffect(() => {
    let cancelled = false;
    setState({ status: "loading" });
    api.getPublicProfile(username)
      .then((profile) => !cancelled && setState({ status: "ready", profile }))
      .catch((err) => !cancelled && setState({ status: err.status === 404 ? "missing" : "error", message: err.message }));
    return () => { cancelled = true; };
  }, [username]);

  useEffect(() => {
    if (state.status === "ready") {
      const p = state.profile;
      document.title = `${p.displayName || p.username} | Dev Portfolio`;
    }
    return () => { document.title = "Dev Portfolio"; };
  }, [state]);

  if (state.status === "loading") return <p className="muted page-pad" aria-live="polite">Loading profile…</p>;
  if (state.status === "missing") {
    return (
      <div className="empty-page">
        <h1 className="display display--small">No page for @{username} yet</h1>
        <p className="lede">Pages appear here once someone logs in with that GitHub account. Check the spelling, or make your own.</p>
        <Link to="/" className="btn">Back to home</Link>
      </div>
    );
  }
  if (state.status === "error") return <p className="form-error page-pad" role="alert">{state.message}</p>;

  const { profile } = state;
  const { activity } = profile;
  const showActivity = activity.syncedAt != null;

  return (
    <article className="profile">
      <header className="profile-head">
        {profile.avatarUrl && <img src={profile.avatarUrl} alt="" className="avatar avatar--large" width="96" height="96" />}
        <div>
          <h1 className="display">{profile.displayName || profile.username}</h1>
          <p className="handle">
            <a href={`https://github.com/${encodeURIComponent(profile.username)}`} rel="noreferrer">
              @{profile.username} on GitHub
            </a>
          </p>
        </div>
      </header>
      {profile.bio && <p className="bio">{profile.bio}</p>}

      {showActivity && (
        <section className="activity" aria-labelledby="activity-heading">
          <h2 id="activity-heading">Recent activity</h2>
          {activity.total > 0 ? (
            <>
              <ActivityHeatmap activity={activity} />
              <div className="activity-detail">
                <p className="activity-sentence">{activitySentence(activity)}</p>
                <ul className="top-repos" aria-label="Most active repositories">
                  {activity.topRepos.map((r) => (
                    <li key={r.repo} style={{ "--share": r.count / activity.topRepos[0].count }}>
                      <span className="top-repo-name">{r.repo}</span>
                      <span className="top-repo-count">{r.count}</span>
                    </li>
                  ))}
                </ul>
              </div>
            </>
          ) : (
            <p className="muted">No public GitHub activity in the last 13 weeks.</p>
          )}
        </section>
      )}

      <section className="work" aria-labelledby="work-heading">
        <h2 id="work-heading">Projects</h2>
        {profile.projects.length === 0 ? (
          <p className="muted">No projects listed yet.</p>
        ) : (
          <ol className="work-list">
            {profile.projects.map((p) => {
              const repo = safeUrl(p.repoUrl);
              const live = safeUrl(p.liveUrl);
              return (
                <li key={p.id} className="work-item">
                  <div className="work-main">
                    <h3>{p.title}</h3>
                    {p.description && <p>{p.description}</p>}
                    <TechChips value={p.techStack} />
                  </div>
                  {(repo || live) && (
                    <div className="work-links">
                      {live && <a href={live} target="_blank" rel="noreferrer">Live site</a>}
                      {repo && <a href={repo} target="_blank" rel="noreferrer">Code</a>}
                    </div>
                  )}
                </li>
              );
            })}
          </ol>
        )}
      </section>
    </article>
  );
}
