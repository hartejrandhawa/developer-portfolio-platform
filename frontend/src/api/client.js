const API_BASE = import.meta.env.VITE_API_BASE || "http://localhost:5000/api";

function getToken() {
  return localStorage.getItem("token");
}

async function request(path, { method = "GET", body, auth = true } = {}) {
  const headers = { "Content-Type": "application/json" };
  if (auth) {
    const token = getToken();
    if (token) headers["Authorization"] = `Bearer ${token}`;
  }

  const res = await fetch(`${API_BASE}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });

  const data = await res.json().catch(() => null);
  if (!res.ok) throw new Error(data?.error || `Request failed (${res.status})`);
  return data;
}

export const api = {
  loginUrl: () => `${API_BASE}/auth/github`,

  getPublicProfile: (username) => request(`/profile/${username}`, { auth: false }),
  updateProfile: (data) => request("/profile", { method: "PUT", body: data }),

  getMyProjects: () => request("/projects"),
  createProject: (data) => request("/projects", { method: "POST", body: data }),
  updateProject: (id, data) => request(`/projects/${id}`, { method: "PUT", body: data }),
  deleteProject: (id) => request(`/projects/${id}`, { method: "DELETE" }),
};
