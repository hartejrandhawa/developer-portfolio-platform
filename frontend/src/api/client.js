import { clearToken, getToken } from "../lib/auth";

// Dev talks to the API on :5000; in production the API serves this app, so
// same-origin "/api" works without CORS.
const API_BASE =
  import.meta.env.VITE_API_BASE || (import.meta.env.DEV ? "http://localhost:5000/api" : "/api");

export class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.status = status;
  }
}

// ASP.NET returns either { error } from our controllers or a ValidationProblem
// ({ errors: { Field: ["msg"] } }) from model validation. Surface the first
// human-readable message from whichever shape came back.
function messageFrom(data, status) {
  if (data?.error) return data.error;
  const firstFieldError = data?.errors && Object.values(data.errors).flat()[0];
  if (firstFieldError) return firstFieldError;
  if (status === 401) return "Your session has ended. Log in again to continue.";
  if (status >= 500) return "The server ran into a problem. Try again in a moment.";
  return data?.title || `Request failed (${status})`;
}

async function request(path, { method = "GET", body, auth = true } = {}) {
  const headers = {};
  if (body !== undefined) headers["Content-Type"] = "application/json";
  if (auth) {
    const token = getToken();
    if (token) headers["Authorization"] = `Bearer ${token}`;
  }

  let res;
  try {
    res = await fetch(`${API_BASE}${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch {
    throw new ApiError("Can't reach the server. Check your connection and try again.", 0);
  }

  if (res.status === 401 && auth) clearToken(); // expired/invalid token: sign out everywhere

  const data = res.status === 204 ? null : await res.json().catch(() => null);
  if (!res.ok) throw new ApiError(messageFrom(data, res.status), res.status);
  return data;
}

export const api = {
  loginUrl: () => `${API_BASE}/auth/github`,

  getPublicProfile: (username) => request(`/profile/${encodeURIComponent(username)}`, { auth: false }),
  getMe: () => request("/me"),
  updateProfile: (data) => request("/profile", { method: "PUT", body: data }),

  getMyProjects: () => request("/projects"),
  createProject: (data) => request("/projects", { method: "POST", body: data }),
  updateProject: (id, data) => request(`/projects/${id}`, { method: "PUT", body: data }),
  deleteProject: (id) => request(`/projects/${id}`, { method: "DELETE" }),
  reorderProjects: (projectIds) => request("/projects/reorder", { method: "PUT", body: { projectIds } }),

  getGitHubRepos: () => request("/github/repos"),
  importRepos: (repoNames) => request("/github/import", { method: "POST", body: { repoNames } }),
  syncActivity: () => request("/github/activity/sync", { method: "POST" }),
};
