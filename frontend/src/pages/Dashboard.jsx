import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "../api/client";

const emptyForm = { title: "", description: "", repoUrl: "", liveUrl: "", techStack: "" };

export default function Dashboard() {
  const [projects, setProjects] = useState([]);
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState(null);
  const [editingDisplayOrder, setEditingDisplayOrder] = useState(0);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    if (!localStorage.getItem("token")) {
      navigate("/");
      return;
    }
    loadProjects();
  }, []);

  async function loadProjects() {
    try {
      const data = await api.getMyProjects();
      setProjects(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  function handleLogout() {
    localStorage.removeItem("token");
    navigate("/");
  }

  async function handleSubmit(e) {
    e.preventDefault();
    setError(null);
    try {
      const payload = {
        title: form.title,
        description: form.description,
        repoUrl: form.repoUrl,
        liveUrl: form.liveUrl,
        techStack: form.techStack,
      };
      if (editingId) {
        await api.updateProject(editingId, { ...payload, displayOrder: editingDisplayOrder });
      } else {
        await api.createProject(payload);
      }
      setForm(emptyForm);
      setEditingId(null);
      setEditingDisplayOrder(0);
      loadProjects();
    } catch (err) {
      setError(err.message);
    }
  }

  function startEdit(project) {
    setEditingId(project.id);
    setEditingDisplayOrder(project.displayOrder);
    setForm({
      title: project.title,
      description: project.description || "",
      repoUrl: project.repoUrl || "",
      liveUrl: project.liveUrl || "",
      techStack: project.techStack || "",
    });
  }

  async function handleDelete(id) {
    try {
      await api.deleteProject(id);
      loadProjects();
    } catch (err) {
      setError(err.message);
    }
  }

  return (
    <div className="page">
      <header className="page-header">
        <h1>Your Dashboard</h1>
        <button className="link-btn" onClick={handleLogout}>Log out</button>
      </header>

      <form className="card" onSubmit={handleSubmit}>
        <h2>{editingId ? "Edit project" : "Add a project"}</h2>
        <input placeholder="Title" value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} required />
        <textarea placeholder="Description" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} rows={3} />
        <input placeholder="Repo URL" value={form.repoUrl} onChange={(e) => setForm({ ...form, repoUrl: e.target.value })} />
        <input placeholder="Live URL (optional)" value={form.liveUrl} onChange={(e) => setForm({ ...form, liveUrl: e.target.value })} />
        <input placeholder="Tech stack (comma-separated)" value={form.techStack} onChange={(e) => setForm({ ...form, techStack: e.target.value })} />
        {error && <p className="error-text">{error}</p>}
        <div className="form-actions">
          <button type="submit">{editingId ? "Save changes" : "Add project"}</button>
          {editingId && <button type="button" className="btn-secondary" onClick={() => { setEditingId(null); setForm(emptyForm); setEditingDisplayOrder(0); }}>Cancel</button>}
        </div>
      </form>

      <div className="card">
        <h2>Your projects</h2>
        {loading ? (
          <p>Loading...</p>
        ) : projects.length === 0 ? (
          <p className="empty-state">No projects yet — add one above.</p>
        ) : (
          <div className="project-list">
            {projects.map((p) => (
              <div key={p.id} className="project-row">
                <div>
                  <h3>{p.title}</h3>
                  <p>{p.description}</p>
                  {p.techStack && <p className="tech-stack">{p.techStack}</p>}
                </div>
                <div className="project-actions">
                  <button onClick={() => startEdit(p)}>Edit</button>
                  <button className="btn-danger" onClick={() => handleDelete(p.id)}>Delete</button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
