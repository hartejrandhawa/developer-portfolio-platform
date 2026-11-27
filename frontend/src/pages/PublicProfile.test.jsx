import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import PublicProfile from "./PublicProfile";
import { api, ApiError } from "../api/client";

vi.mock("../api/client", async (importOriginal) => {
  const actual = await importOriginal();
  return { ...actual, api: { getPublicProfile: vi.fn() } };
});

const activity = {
  from: "2026-08-29", to: "2026-11-27", total: 7, commits: 5, pullRequests: 2, issues: 0,
  days: [{ date: "2026-11-26", count: 7 }], topRepos: [{ repo: "alice/site", count: 7 }], syncedAt: "2026-11-27T10:00:00",
};

function renderAt(path) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes><Route path="/u/:username" element={<PublicProfile />} /></Routes>
    </MemoryRouter>,
  );
}

describe("PublicProfile", () => {
  it("shows the person, their activity and projects in order", async () => {
    api.getPublicProfile.mockResolvedValue({
      username: "alice", displayName: "Alice Liddell", bio: "Builds tools.", avatarUrl: null, activity,
      projects: [
        { id: 2, title: "First", description: "One", repoUrl: "https://github.com/alice/first", liveUrl: null, techStack: "Go,Rust", displayOrder: 0 },
        { id: 1, title: "Second", description: null, repoUrl: null, liveUrl: "https://second.dev", techStack: null, displayOrder: 1 },
      ],
    });

    renderAt("/u/alice");

    expect(await screen.findByRole("heading", { level: 1, name: "Alice Liddell" })).toBeInTheDocument();
    expect(screen.getByText("Builds tools.")).toBeInTheDocument();
    expect(screen.getByText("5 commits and 2 pull requests across 1 repository in the last 13 weeks.")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: /7 contributions between/ })).toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 3 }).map((h) => h.textContent)).toEqual(["First", "Second"]);
    expect(screen.getByRole("link", { name: "Code" })).toHaveAttribute("href", "https://github.com/alice/first");
    expect(screen.getByRole("link", { name: "Live site" })).toHaveAttribute("href", "https://second.dev/");
    expect(screen.getByText("Rust")).toBeInTheDocument();
  });

  it("never renders an unsafe URL as a link, even if the API returns one", async () => {
    api.getPublicProfile.mockResolvedValue({
      username: "mallory", displayName: null, bio: null, avatarUrl: null, activity: { ...activity, syncedAt: null },
      projects: [{ id: 1, title: "Totally safe", description: null, repoUrl: "javascript:alert(1)", liveUrl: "data:text/html,x", techStack: null, displayOrder: 0 }],
    });

    renderAt("/u/mallory");

    expect(await screen.findByText("Totally safe")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Code" })).toBeNull();
    expect(screen.queryByRole("link", { name: "Live site" })).toBeNull();
    expect(screen.queryByText("Recent activity")).toBeNull(); // never synced
  });

  it("explains when a profile doesn't exist", async () => {
    api.getPublicProfile.mockRejectedValue(new ApiError("Profile not found", 404));
    renderAt("/u/ghost");
    expect(await screen.findByRole("heading", { name: "No page for @ghost yet" })).toBeInTheDocument();
  });
});
