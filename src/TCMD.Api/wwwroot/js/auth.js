import{api,refreshAntiforgery}from"./api-client.js";
let current=null;export const session=()=>current;export const isOperational=()=>current&&current.role!=="Instructor";export const isAdmin=()=>current?.role==="Administrator";
export async function restore(){const result=await api("/api/auth/session");current=result.ok?result.data:null;return result}
export async function login(userName,password){const result=await api("/api/auth/login",{method:"POST",body:{userName,password},login:true});if(!result.ok)return result;await refreshAntiforgery();return restore()}
export async function logout(){await refreshAntiforgery();const result=await api("/api/auth/logout",{method:"POST"});if(!result.ok)return result;current=null;await refreshAntiforgery();return result}
export function clearSession(){current=null}
