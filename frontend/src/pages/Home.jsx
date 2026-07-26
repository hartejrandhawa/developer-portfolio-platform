import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "../api/client";

export default function Home() {
  const [username, setUsername] = useState("");
  const navigate = useNavigate();
  const isLoggedIn = !!localStorage.getItem("token");

  function handleViewProfile(e) {
    e.preventDefault();
    if (username.trim()) navigate(`/u/${username.trim()}`);
  }

  return (
    <div className="page hero">
      <h1>Dev Portfolio</h1>
      <p className="subtitle">
        Connect your GitHub, showcase your projects, and get a shareable
        profile page — no separate account or password needed.
      </p>

      {isLoggedIn ? (
        <a className="btn-primary" href="/dashboard">Go to your dashboard</a>
      ) : (
        <a className="btn-primary" href={api.loginUrl()}>Log in with GitHub</a>
      )}

      <form className="view-profile-form" onSubmit={handleViewProfile}>
        <input
          placeholder="View a profile by username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
        />
        <button type="submit">View</button>
      </form>
    </div>
  );
}
