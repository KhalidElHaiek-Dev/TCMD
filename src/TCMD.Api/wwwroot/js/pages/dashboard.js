import {api} from "../api-client.js";
import {el} from "../dom.js";
import {pageHeader, loading, empty, badge, table} from "../components.js";
import {date} from "../formatters.js";
import {problemView} from "../problem-details.js";
export async function dashboardPage(identity) {
  const root = el(
            "div", {},
            pageHeader(
                {title: "Dashboard", description: "Operational overview of the training center."})),
        content = el("div", {}, loading());
  root.append(content);
  if (identity.role === "Instructor")
    await renderInstructor(content, identity);
  else
    await renderOperational(content, identity);
  return root
}
async function renderOperational(content, identity) {
  const [students, courses, groups] = await Promise.all(
      [api("/api/students?isActive=true"), api("/api/courses"), api("/api/training-groups")]);
  const failed = [students, courses, groups].find(x => !x.ok);
  if (failed) {
    content.replaceChildren(problemView(failed.problem, failed.status));
    return
  }
  const activeCourses = courses.data.filter(x => x.isActive),
        activeGroups = groups.data.filter(x => x.status === "Active"),
        plannedGroups = groups.data.filter(x => x.status === "Planned"),
        courseNames = new Map(courses.data.map(x => [x.id, `${x.code} — ${x.name}`])),
        attention = plannedGroups.filter(x => !x.primaryInstructorId), actions = [
          action("Add Student", "#/students/new"), action("Create Training Group", "#/groups/new"),
          action("Add Course", "#/courses/new"), action("Add Instructor", "#/instructors/new")
        ];
  if (identity.role === "Administrator")
    actions.push(action("Staff Accounts", "#/staff-accounts"));
  content.replaceChildren(
      el("section", {"aria-labelledby": "dashboard-metrics"},
         el("h2", {class: "sr-only", id: "dashboard-metrics", text: "Key metrics"}),
         el("div", {class: "metric-grid"}, metric("Active Students", students.data.length),
            metric("Active Training Groups", activeGroups.length),
            metric("Active Courses", activeCourses.length),
            metric("Planned Training Groups", plannedGroups.length))),
      section(
          "Groups requiring attention",
          attention.length ?
              table(
                  "Planned Training Groups without a primary Instructor",
                  [
                    {label: "Group name", render: x => el("strong", {text: x.name})}, {
                      label: "Course",
                      render: x => courseNames.get(x.courseId) ?? "Course unavailable"
                    },
                    {
                      label: "Planned dates",
                      render: x => `${date(x.plannedStartDate)} – ${date(x.plannedEndDate)}`
                    },
                    {label: "Status", render: x => badge(x.status)}, {
                      label: "Actions",
                      render: x => el("a", {href: `#/groups/${x.id}`, text: "View"})
                    }
                  ],
                  attention) :
              empty("No groups currently require an Instructor."),
          "attention-section"),
      section("Quick actions", el("div", {class: "quick-actions"}, actions)))
}
async function renderInstructor(content, identity) {
  if (identity.instructorLinkStatus !== "Active") {
    const message = identity.instructorLinkStatus === "Unlinked" ?
        "No Instructor record is linked to this account. Ask an Administrator to create the link." :
        "The linked Instructor record is inactive. Ask an Administrator for assistance.";
    content.replaceChildren(section(
        "Instructor account",
        el("div", {class: "alert alert-warning", role: "status", text: message})));
    return
  }
  const groups = await api("/api/training-groups");
  if (!groups.ok) {
    content.replaceChildren(problemView(groups.problem, groups.status));
    return
  }
  content.replaceChildren(
      el("div", {class: "metric-grid instructor-metrics"},
         metric("Account status", "Linked and active"),
         metric("Assigned Groups", groups.data.length)),
      section(
          "Assigned Groups",
          groups.data.length ?
              table(
                  "Assigned Training Groups",
                  [
                    {label: "Group name", render: x => el("strong", {text: x.name})},
                    {label: "Status", render: x => badge(x.status)}, {
                      label: "Course",
                      render: () => el("span", {class: "muted", text: "Course details unavailable"})
                    },
                    {
                      label: "Planned dates",
                      render: x => `${date(x.plannedStartDate)} – ${date(x.plannedEndDate)}`
                    },
                    {
                      label: "Actions",
                      render: x => el("a", {href: `#/groups/${x.id}`, text: "View"})
                    }
                  ],
                  groups.data) :
              empty("No training groups are currently assigned to you.")))
}
function metric(label, value) {
  return el(
      "article", {class: "metric-card"}, el("div", {class: "metric-label", text: label}),
      el("div", {class: "metric-value", text: String(value)}))
}
function section(title, body, className = "") {
  return el(
      "section", {class: `dashboard-section ${className}`.trim()}, el("h2", {text: title}), body)
}
function action(label, href) {
  return el("a", {class: "button secondary", href, text: label})
}
