import { useSyncExternalStore } from "react";

// Token lives in localStorage so it survives reloads. Components subscribe via
// useAuthToken(), so logging out anywhere (including an API 401) updates the
// nav and redirects without each page polling localStorage.
const KEY = "token";
const EVENT = "auth-change";

export function getToken() {
  return localStorage.getItem(KEY);
}

export function setToken(token) {
  localStorage.setItem(KEY, token);
  window.dispatchEvent(new Event(EVENT));
}

export function clearToken() {
  localStorage.removeItem(KEY);
  window.dispatchEvent(new Event(EVENT));
}

function subscribe(callback) {
  window.addEventListener(EVENT, callback);
  window.addEventListener("storage", callback); // other tabs
  return () => {
    window.removeEventListener(EVENT, callback);
    window.removeEventListener("storage", callback);
  };
}

export function useAuthToken() {
  return useSyncExternalStore(subscribe, getToken);
}
