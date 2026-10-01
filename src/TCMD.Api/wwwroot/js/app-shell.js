import {el} from "./dom.js";
const mobileNavigation = window.matchMedia("(max-width: 768px)");
export function shell(identity, onLogout) {
  const root = el("div", {class: "app-shell"});
  const links =
      identity.role === "Instructor" ? [["/dashboard", "Dashboard"], ["/groups", "My Groups"]] : [
        ["/dashboard", "Dashboard"], ["/students", "Students"], ["/instructors", "Instructors"],
        ["/courses", "Courses"], ["/groups", "Training Groups"]
      ];
  if (identity.role === "Administrator")
    links.push(["/staff-accounts", "Staff Accounts"]);
  const logoutStatus = el("div", {class: "logout-status", "aria-live": "polite"}),
        logoutButton = el("button", {
          class: "sidebar-logout",
          type: "button",
          text: "Sign out",
          onclick: () => onLogout(logoutButton, logoutStatus)
        });
  const nav =
      el("nav", {class: "nav", "aria-label": "Primary"},
         el("div", {class: "nav-inner"},
            links.map(
                ([href, label]) => el("a", {href: `#${href}`, "data-route": href, text: label}))));
  const closeButton =
      el("button",
         {class: "drawer-close", type: "button", "aria-label": "Close navigation", text: "×"});
  const sidebar =
      el("aside", {
        class: "sidebar",
        id: "primary-navigation",
        tabindex: "-1",
        "aria-labelledby": "navigation-title"
      },
         el("div", {class: "sidebar-header"},
            el("a", {class: "brand", href: "#/dashboard", id: "navigation-title", text: "TCMD"}),
            closeButton),
         nav,
         el("div", {class: "sidebar-account"},
            el("div", {class: "identity-name", text: identity.displayName}),
            el("div", {class: "identity-role", text: identity.role}), logoutButton, logoutStatus));
  const menuButton = el("button", {
    class: "menu-button",
    type: "button",
    "aria-controls": "primary-navigation",
    "aria-expanded": "false",
    "aria-label": "Open navigation",
    text: "☰"
  }),
        main = el("main", {id: "main-content", tabindex: "-1"});
  const workspace =
      el("div", {class: "workspace"},
         el("header", {class: "top-bar"}, menuButton,
            el("a", {class: "top-bar-brand", href: "#/dashboard", text: "TCMD"}),
            el("span", {class: "top-bar-role", text: identity.role})),
         el("div", {class: "content-container"}, main));
  let open = false;
  function setDrawer(next, {restoreFocus = false} = {}) {
    open = mobileNavigation.matches && next;
    root.classList.toggle("drawer-open", open);
    menuButton.setAttribute("aria-expanded", String(open));
    menuButton.setAttribute("aria-label", open ? "Close navigation" : "Open navigation");
    if (mobileNavigation.matches) {
      sidebar.setAttribute("aria-hidden", String(!open));
      sidebar.inert = !open
    } else {
      sidebar.removeAttribute("aria-hidden");
      sidebar.inert = false
    }
    if (open)
      closeButton.focus();
    else if (restoreFocus)
      menuButton.focus()
  }
  menuButton.addEventListener("click", () => setDrawer(!open, {restoreFocus: open}));
  closeButton.addEventListener("click", () => setDrawer(false, {restoreFocus: true}));
  nav.addEventListener("click", event => {
    if (open && event.target.closest("a"))
      setDrawer(false, {restoreFocus: true})
  });
  root.addEventListener("keydown", event => {
    if (!open)
      return;
    if (event.key === "Escape") {
      event.preventDefault();
      setDrawer(false, {restoreFocus: true});
      return
    }
    if (event.key !== "Tab")
      return;
    const focusable = [...sidebar.querySelectorAll('a[href],button:not([disabled])')],
          first = focusable[0], last = focusable.at(-1);
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus()
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus()
    }
  });
  mobileNavigation.addEventListener("change", () => setDrawer(false));
  root.append(sidebar, workspace);
  setDrawer(false);
  return {
    root, main, nav
  }
}
export function markRoute(nav, path) {
  nav.querySelectorAll("a[data-route]").forEach(a => {
    const active = path === a.dataset.route || path.startsWith(`${a.dataset.route}/`);
    active ? a.setAttribute("aria-current", "page") : a.removeAttribute("aria-current")
  })
}
