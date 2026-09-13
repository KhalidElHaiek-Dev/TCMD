export function route(){const raw=(location.hash||"#/dashboard").slice(1);const[url,query=""]=raw.split("?");return {path:url||"/dashboard",segments:(url||"/dashboard").split("/").filter(Boolean),query:new URLSearchParams(query)}}
export function go(path){location.hash=path.startsWith("/")?path:`/${path}`}
export function start(handler){window.addEventListener("hashchange",handler);handler()}
