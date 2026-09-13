# Product Scope

## Product objective

TCMD V1 is an internal web application for one training center. It gives authorized staff one place to manage the people, courses, groups, sessions, enrollments, and attendance involved in daily training operations.

## Problems TCMD solves

TCMD aims to reduce these common problems:

- student and instructor information stored in unrelated files;
- no clear view of who belongs to each training group;
- difficulty connecting a group to its course and instructor;
- session schedules that are hard to track;
- inconsistent or missing attendance records; and
- staff having access to actions they should not perform.

## Version 1 users

V1 is for internal users only:

- administrators;
- staff members; and
- instructors.

Students will not have accounts or sign in during V1.

## Version 1 features

### Authentication and access

- Staff users can sign in and sign out.
- The system restricts actions using basic roles.
- An administrator can create, deactivate, and assign roles to staff accounts.
- Protected information is unavailable to users who are not signed in.

V1 uses administrator-assisted password reset. Self-service password recovery and outbound recovery email are deferred.

### Students

- View and search students.
- Add a student.
- View a student's details and enrollments.
- Update a student's basic details.
- Mark a student inactive when the record should no longer be used.
- Require a phone number.
- Automatically generate a unique student number in the format `STU-000001`.

Email is optional. Additional personal and contact fields are outside the minimum V1 record.

### Instructors

- View and search instructors.
- Add an instructor.
- View and update an instructor's basic details.
- Mark an instructor inactive.
- View the groups assigned to an instructor.

An instructor record does not require a staff sign-in account. It may optionally link to one account.
Only Administrators manage the optional one-to-one link, and a linked account must have the Instructor role.

### Courses

- View and search courses.
- Add a course with a name, code, and description.
- Update course information.
- Mark a course inactive.
- View groups that deliver a course.

Course prices, formal curricula, prerequisites, and course materials are not part of V1.

### Training groups or cohorts

- Create a group for a course.
- Give the group a name and planned start and end dates.
- Assign at most one primary instructor. A Planned group may have none, but an instructor is required before activation.
- Update group information and status.
- View the students enrolled in a group.
- View the group's sessions.

V1 supports at most one primary instructor per group. Multiple instructors are deferred.

### Enrollments

- Enroll an active student in an active or planned group.
- View enrollments by student and by group.
- Change an enrollment status.
- Prevent the same student from being enrolled in the same group twice.
- Reactivate the existing enrollment when a withdrawn student rejoins the same group.

Enrollment statuses are Active, Completed, and Withdrawn.

### Training sessions

- Schedule a session for a group.
- Record its date, start time, and end time.
- View and update scheduled sessions.
- Cancel a session without deleting its history.
- Keep sessions within the group's planned date range.
- Store an optional plain-text location.

Structured room management and schedule-conflict detection are deferred.

### Attendance

- View enrolled students for a session.
- Record one attendance status for each enrolled student.
- Correct an attendance record.
- View attendance for a session, student, or group.
- Prevent attendance from being recorded before the session starts.

Attendance statuses are Present, Absent, Late, and Excused. A correction note is optional. Full correction-history auditing is deferred.

Missing attendance means Not Recorded, not Absent. New attendance requires a currently Active enrollment in the
session's group whose enrollment date is no later than the session date. A Scheduled session accepts attendance once
its Africa/Casablanca local start is reached; a Completed session accepts it without a clock restriction, while a
Cancelled session rejects new entry. Existing attendance remains readable and correctable after later status changes.
Correction notes are trimmed optional text limited to 1,000 characters.

### Basic operational views

V1 may show simple lists and counts needed for the workflows above. Examples include active students, upcoming sessions, group membership, and attendance by session. Advanced analytics and custom report building are postponed.

Linked, active Instructor accounts see only groups currently assigned to their Instructor record and those groups'
sessions, compact enrollment rosters, and Attendance. Compact Student data contains identifier, student number, name,
and active status; Student phone and email remain unavailable. Current primary-Instructor assignment also controls
historical access because V1 has no per-session Instructor history.

## Explicitly postponed

The following features are outside V1:

- online course videos or a learning-management system;
- online payments;
- mobile applications;
- chat;
- notifications;
- certificates;
- multiple training centers;
- SaaS subscriptions;
- marketplace features;
- microservices;
- CQRS;
- message brokers;
- Redis;
- Docker;
- AI features inside TCMD;
- student accounts or a student portal;
- advanced analytics and custom report building;
- file exports unless later approved; and
- financial management, fees, invoices, or payment records.

All financial tracking remains outside V1.

## V1 success criteria

V1 is successful when authorized staff can:

1. Sign in and access only actions permitted by their role.
2. Maintain reliable student, instructor, and course records.
3. Create groups and enroll students without duplicate enrollment.
4. Schedule group sessions.
5. Record and correct attendance for enrolled students.
6. Find the current information for a student, group, instructor, or session.
7. Complete these workflows with server-side validation and integration-test coverage.

Hosting, production backup ownership, and release approval are deferred until release preparation. V1 is designed for a small internal workload; it does not require special scale infrastructure.
