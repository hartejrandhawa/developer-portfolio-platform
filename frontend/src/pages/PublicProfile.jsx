import { useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { api } from "../api/client";

export default function PublicProfile() {
  const { username } = useParams();
  const [profile, setProfile] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    api.getPublicProfile(username)
      .then(setProfile)
      .catch((err) => setError(err.message));
  }, [username]);

  if (error) return <div className="page"><p className="error-text">{error}</p></div>;
  if (!profile) return <div className="page"><p>Loading...</p></div>;

  return (
    <div className="page">
      <Link to="/" className="back-link">&larr; Home</Link>
      <header className="profile-header">
        {profile.avatarUrl && <img src={profile.avatarUrl} alt={profile.username} className="avatar" />}
        <div>
          <h1>{profile.displayName || profile.username}</h1>
          <p className="subtitle">@{profile.username}</p>
          {profile.bio && <p>{profile.bio}</p>}
        </div>
      </header>

      <div className="project-list">
        {profile.projects.map((p) => (
          <div key={p.id} className="card project-card">
            <h3>{p.title}</h3>
            <p>{p.description}</p>
            {p.techStack && <p className="tech-stack">{p.techStack}</p>}
            <div className="project-links">
              {p.repoUrl && <a href={p.repoUrl} target="_blank" rel="noreferrer">Repo</a>}
              {p.liveUrl && <a href={p.liveUrl} target="_blank" rel="noreferrer">Live demo</a>}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
