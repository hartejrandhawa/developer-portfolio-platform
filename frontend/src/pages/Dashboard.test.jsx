import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import Dashboard from "./Dashboard";
import { api } from "../api/client";
import { setToken } from "../lib/auth";

vi.mock("../api/client", async (importOriginal) => {
  const actual = await importOriginal();
  return {
    ...actual,
    api: {
      getMe: vi.fn(), getMyProjects: vi.fn(), syncActivity: vi.fn(), createProject: vi.fn(),
      reorderProjects: vi.fn(), deleteProject: vi.fn(), updateProfile: vi.fn(),
    },
  };
});

const me = { username: "alice", displayName: "Alice", bio: null, avatarUrl: null, activitySyncedAt: null };
const activity = { from: "2026-08-29", to: "2026-11-27", total: 0, commits: 0, pullRequests: 0, issues: 0, days: [], topRepos: [], syncedAt: null };
const project = (id, title, displayOrder) => ({ id, title, description: null, repoUrl: null, liveUrl: null, techStack: null, displayOrder });

function renderDashboard() {
  return render(
    <MemoryRouter initialEntries={["/dashboard"]}>
      <Routes>
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/" element={<p>home page</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("Dashboard", () => {
  beforeEach(() => {
    setToken("t");
    api.getMe.mockResolvedValue(me);
    api.syncActivity.mockResolvedValue(activity);
  });

  it("sends visitors without a session home", async () => {
    localStorage.clear();
    renderDashboard();
    expect(await screen.findByText("home page")).toBeInTheDocument();
  });

  it("adds a project through the form", async () => {
    api.getMyProjects.mockResolvedValue([]);
    api.createProject.mockImplementation(async (form) => ({ ...project(9, form.title, 0), techStack: form.techStack }));

    renderDashboard();
    expect(await screen.findByText(/no projects yet/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Add project" }));
    fireEvent.change(screen.getByLabelText("Title"), { target: { value: "Ray tracer" } });
    fireEvent.change(screen.getByLabelText("Built with"), { target: { value: "Rust, WGSL" } });
    fireEvent.click(within(screen.getByRole("heading", { name: "New project" }).parentElement)
      .getByRole("button", { name: "Add project" }));

    expect(await screen.findByRole("heading", { level: 3, name: "Ray tracer" })).toBeInTheDocument();
    expect(api.createProject).toHaveBeenCalledWith(expect.objectContaining({ title: "Ray tracer", techStack: "Rust, WGSL" }));
    expect(screen.getByRole("status")).toHaveTextContent("Project added");
  });

  it("reorders optimistically and rolls back if the server refuses", async () => {
    api.getMyProjects.mockResolvedValue([project(1, "A", 0), project(2, "B", 1)]);
    api.reorderProjects.mockRejectedValueOnce(new Error("projectIds must list each of your projects exactly once"));

    renderDashboard();
    fireEvent.click(await screen.findByRole("button", { name: "Move B up" }));

    expect(api.reorderProjects).toHaveBeenCalledWith([2, 1]);
    expect(await screen.findByRole("alert")).toHaveTextContent("exactly once");
    const titles = screen.getAllByRole("heading", { level: 3 }).map((h) => h.textContent);
    expect(titles).toEqual(["A", "B"]);
  });

  it("asks before deleting", async () => {
    api.getMyProjects.mockResolvedValue([project(1, "Doomed", 0)]);
    api.deleteProject.mockResolvedValue(null);

    renderDashboard();
    fireEvent.click(await screen.findByRole("button", { name: "Delete" }));
    expect(api.deleteProject).not.toHaveBeenCalled();
    expect(screen.getByText("Delete this project?")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(screen.queryByText("Doomed")).toBeNull());
    expect(api.deleteProject).toHaveBeenCalledWith(1);
  });
});
