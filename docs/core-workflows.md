# Core Workflows

This document describes the main V1 business workflows. Exact screen layouts and API endpoints will be designed later.

## 1. Staff sign-in

1. The user enters their sign-in details.
2. TCMD verifies the account and password.
3. TCMD rejects invalid or inactive accounts without revealing sensitive details.
4. TCMD starts an authenticated session for a valid account.
5. TCMD allows only the actions permitted by the user's role.
6. The user can sign out and end the session.

Password reset is performed by an administrator. Self-service recovery is deferred.

## 2. Manage a student

1. An administrator or staff member opens the student list.
2. They search for an existing student before adding a new record.
3. They add the student's required basic details.
4. TCMD validates the data and creates the student.
5. Authorized users can later update the details.
6. If the student should no longer be used, staff mark the record inactive.

Normal V1 workflows do not permanently delete students. Staff mark a student inactive, and TCMD preserves all enrollment and attendance history.

## 3. Manage an instructor

1. An administrator or staff member searches the instructor list.
2. They create an instructor if the person does not already exist.
3. They enter and save the required basic details.
4. They may assign the instructor as the primary instructor of a group.
5. They can update or deactivate the instructor later.

An inactive instructor cannot be assigned to a new group. Existing group assignments and their history remain intact.

## 4. Manage a course

1. An administrator or staff member searches existing courses.
2. They create a course with a unique code, name, and optional description.
3. They update the course when its descriptive information changes.
4. They may mark a course inactive when it is no longer offered.

An inactive course cannot be selected for a new group. Existing groups keep their course history.

## 5. Create and manage a training group

1. An administrator or staff member selects an active course.
2. They enter the group name and planned dates.
3. They may assign one active primary instructor while the group is Planned. A primary instructor is required before activation.
4. TCMD validates that the end date is not before the start date.
5. TCMD creates the group as Planned. Activation is a separate action and requires both the course and primary instructor to be active.
6. Staff can update the group, view its members, and view its sessions.

Group statuses are Planned, Active, Completed, and Cancelled. A group moves from Planned to Active and then to Completed, or from Planned or Active to Cancelled. Completed and Cancelled are terminal statuses in V1.

While Planned, a group may change course and may assign, replace, or remove its primary instructor. While Active, its name and dates may be updated and its primary instructor may be replaced by another active instructor, but its course cannot change and its instructor cannot be removed. Completed and Cancelled groups cannot be edited. Cancellation preserves the group and its history rather than deleting it.

## 6. Enroll a student

1. An administrator or staff member opens a group.
2. They select an active student.
3. TCMD checks that the student is not already enrolled in that group.
4. TCMD checks that the group can accept enrollments.
5. TCMD creates the enrollment with an initial status.
6. Staff can later mark the enrollment Completed or Withdrawn.
7. If a withdrawn student rejoins the same group, staff reactivate the existing enrollment instead of creating another record.

Enrollment statuses are Active, Completed, and Withdrawn. Group capacity limits are deferred.

Changing an enrollment must not erase its earlier attendance records.
New enrollment and withdrawn-enrollment reactivation require an active student and a Planned or Active group. Completed enrollments are terminal. Completing or cancelling a group does not automatically change its enrollments, although staff may still complete or withdraw an enrollment afterward as a correction. The system assigns the immutable enrollment date from the current UTC date when the enrollment is first created.

## 7. Schedule a training session

1. An administrator or staff member opens a group.
2. They enter the session date, start time, and end time.
3. TCMD checks that the end is later than the start.
4. TCMD checks that the session date falls within the group's planned dates.
5. TCMD creates the scheduled session with an optional plain-text location.
6. An administrator or staff member can update or cancel it. Instructors can only view their assigned sessions.

V1 does not detect instructor, room, or group scheduling conflicts. Structured room management and conflict detection are deferred.

A cancelled session remains in history and cannot receive normal attendance records.

## 8. Record attendance

1. An authorized user opens a session.
2. TCMD confirms that the session has started and is not cancelled.
3. TCMD shows students with applicable enrollments in the session's group.
4. The user selects one attendance status for each student.
5. TCMD validates that each attendance record belongs to that session and an applicable enrollment.
6. TCMD saves at most one attendance record per enrollment per session.
7. An authorized user can correct a saved status and may add an optional correction note.

Attendance statuses are Present, Absent, Late, and Excused. Attendance cannot be recorded before the session starts. A correction note is optional, and full correction-history auditing is deferred.

## 9. View operational information

Authorized users can search or navigate to:

- a student and their enrollments;
- an instructor and their assigned groups;
- a course and its groups;
- a group, its enrolled students, and its sessions;
- a session and its attendance; and
- a student's attendance within their enrollments.

Instructors only see information permitted by the confirmed role rules.

## Common validation and error behavior

- Required values must be present.
- Codes that are defined as unique cannot be duplicated.
- Dates and times must form valid ranges.
- References must point to existing records.
- Inactive records cannot be selected for new work unless a rule explicitly permits it.
- Duplicate enrollments and duplicate attendance records are rejected.
- Unauthorized actions are rejected without changing data.
- Validation messages should explain what the user can correct without exposing technical or sensitive information.
- Important mutable records use SQL Server `rowversion`. When another user has changed the record, TCMD rejects the stale update and asks the user to reload instead of silently overwriting data.
