import { StrictMode } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import AuthCallback from "./AuthCallback";
import { getToken } from "../lib/auth";

function renderCallback() {
  return render(
    <StrictMode>
      <MemoryRouter initialEntries={["/auth/callback"]}>
        <Routes>
          <Route path="/auth/callback" element={<AuthCallback />} />
          <Route path="/dashboard" element={<p>dashboard</p>} />
          <Route path="/" element={<p>home</p>} />
        </Routes>
      </MemoryRouter>
    </StrictMode>,
  );
}

describe("AuthCallback", () => {
  afterEach(() => {
    window.history.replaceState(null, "", "/");
  });

  it("stores the token from the fragment and opens the dashboard", async () => {
    window.history.replaceState(null, "", "/auth/callback#token=abc.def");
    renderCallback();
    expect(await screen.findByText("dashboard")).toBeInTheDocument();
    expect(getToken()).toBe("abc.def");
  });

  it("still lands on the dashboard when the effect re-runs after the fragment is gone", async () => {
    // Regression: StrictMode's second effect run saw an empty fragment (the
    // first run had already navigated) and bounced to /?error=login_failed.
    window.history.replaceState(null, "", "/auth/callback#token=abc.def");
    const { unmount } = renderCallback();
    await screen.findByText("dashboard");
    unmount();

    window.history.replaceState(null, "", "/auth/callback");
    renderCallback();
    expect(await screen.findByText("dashboard")).toBeInTheDocument();
  });

  it("reports a failed login when there is no token at all", async () => {
    window.history.replaceState(null, "", "/auth/callback");
    renderCallback();
    expect(await screen.findByText("home")).toBeInTheDocument();
    expect(getToken()).toBeNull();
  });
});
