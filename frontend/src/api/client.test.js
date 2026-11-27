import { describe, expect, it, vi } from "vitest";
import { api } from "./client";
import { getToken, setToken } from "../lib/auth";

function respond(status, body) {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
    ok: status >= 200 && status < 300,
    status,
    json: () => (body === undefined ? Promise.reject(new Error("no body")) : Promise.resolve(body)),
  }));
}

describe("api client", () => {
  it("sends the bearer token on authenticated calls", async () => {
    setToken("abc");
    respond(200, []);
    await api.getMyProjects();
    expect(fetch.mock.calls[0][1].headers.Authorization).toBe("Bearer abc");
  });

  it("does not send the token on public calls", async () => {
    setToken("abc");
    respond(200, { username: "a" });
    await api.getPublicProfile("a b");
    const [url, init] = fetch.mock.calls[0];
    expect(url).toMatch(/\/profile\/a%20b$/);
    expect(init.headers.Authorization).toBeUndefined();
  });

  it("clears the token when the API says 401", async () => {
    setToken("expired");
    respond(401, undefined);
    await expect(api.getMe()).rejects.toMatchObject({ status: 401 });
    expect(getToken()).toBeNull();
  });

  it("surfaces the first ASP.NET validation message", async () => {
    respond(400, { title: "One or more validation errors occurred.", errors: { RepoUrl: ["Code link must be a full http:// or https:// URL."] } });
    await expect(api.createProject({ title: "x" })).rejects.toThrow("Code link must be a full http:// or https:// URL.");
  });

  it("explains server errors without exposing a bare status code", async () => {
    respond(500, undefined);
    await expect(api.getPublicProfile("a")).rejects.toThrow("The server ran into a problem");
  });

  it("returns null for 204 responses", async () => {
    respond(204, undefined);
    await expect(api.deleteProject(1)).resolves.toBeNull();
  });

  it("reports network failures in plain language", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    await expect(api.getMyProjects()).rejects.toThrow("Can't reach the server");
  });
});
