import {api, refreshAntiforgery, clearAntiforgery} from "./api-client.js";
let current = null;
export const session = () => current;
export const isOperational = () => current && current.role !== "Instructor";
export const isAdmin = () => current?.role === "Administrator";
export async function restore() {
  const result = await api("/api/auth/session");
  if (result.ok)
    current = result.data;
  else if (result.status === 401)
    current = null;
  return result
}
export async function login(userName, password) {
  const security = await refreshAntiforgery();
  if (!security.ok)
    return security;
  const result =
      await api("/api/auth/login", {method: "POST", body: {userName, password}, login: true});
  if (!result.ok)
    return result;
  const refreshed = await refreshAntiforgery();
  if (!refreshed.ok)
    return refreshed;
  return restore()
}
export async function logout() {
  const security = await refreshAntiforgery();
  if (!security.ok)
    return security;
  const result = await api("/api/auth/logout", {method: "POST"});
  if (!result.ok)
    return result;
  current = null;
  clearAntiforgery();
  await refreshAntiforgery();
  return result
}
export function clearSession() {
  current = null;
  clearAntiforgery()
}
