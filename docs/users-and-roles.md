# Users and Roles

## Access model

TCMD V1 is an internal system. Every user must sign in with an active staff account. Access is controlled by a small set of roles.

V1 has three proposed roles:

1. Administrator
2. Staff
3. Instructor

The application must enforce permissions on the server. Hiding a button in the browser is not sufficient security.

## Administrator

An administrator manages the system and can:

- perform all operational actions available to staff;
- create staff accounts;
- activate or deactivate staff accounts;
- assign or change roles; and
- correct operational records when authorized.

V1 must always have at least one active administrator. The first administrator is created through a documented one-time initialization process using credentials supplied through user secrets or environment variables. Public registration and source-controlled default credentials are forbidden.

The system prevents an administrator from deactivating their own account and prevents deactivation or demotion of the last active administrator.

## Staff

A staff member handles normal training-center administration and can:

- manage students and instructors;
- manage courses;
- create and update training groups;
- assign a primary instructor to a group;
- enroll students and update enrollment status;
- schedule, update, and cancel sessions;
- record and correct attendance; and
- view operational lists and details.

A staff member cannot create staff accounts, change roles, or manage account access.

## Instructor

An instructor has limited access related to assigned work and can:

- view their assigned groups;
- view students enrolled in those groups;
- view sessions for those groups; and
- record or correct attendance for their assigned groups.

Instructors cannot update student details. They cannot create, reschedule, or cancel sessions. They cannot see groups or students that are not assigned to them.

Milestone 9 does not yet expose session reads to authenticated Instructor-role accounts. The optional StaffUser-to-Instructor link is not implemented, so all current Training Session endpoints require the Operational Staff policy. Assigned-session reads remain deferred until that link can enforce the approved restriction without exposing other instructors' records.

Milestone 10 likewise restricts all Attendance endpoints to the Operational Staff policy. Instructor Attendance access
remains deferred until account-to-Instructor linkage supports enforceable assigned-group authorization.

## Staff accounts and instructor records

A staff account represents permission to sign in. An instructor record represents a person who teaches.

These are separate concepts because an instructor might exist in scheduling records without being allowed to sign in. A staff account may optionally be linked to one instructor record.

An instructor does not automatically receive an account. A staff account may optionally link to one instructor record, and an instructor record may link to at most one staff account.

## Basic access table

| Action | Administrator | Staff | Instructor |
|---|---:|---:|---:|
| Sign in and sign out | Yes | Yes | Yes |
| View operational records | Yes | Yes | Assigned records only |
| Manage students | Yes | Yes | No; view assigned students only |
| Manage instructors | Yes | Yes | No |
| Manage courses | Yes | Yes | No |
| Manage groups | Yes | Yes | View assigned groups |
| Manage enrollments | Yes | Yes | View assigned groups |
| Manage sessions | Yes | Yes | View assigned sessions only |
| Record attendance | Yes | Yes | Assigned groups only |
| Manage staff accounts and roles | Yes | No | No |

## Account and authorization rules

- A deactivated account cannot sign in.
- A signed-in user's current role controls access.
- The server checks authorization for every protected operation.
- Staff passwords must never be stored as plain text.
- Sensitive sign-in information must not be written to normal application logs.
- Failed authorization must not change data.
- Role names should remain few and stable during V1.

V1 uses ASP.NET Core Identity with its secure password hashing. Passwords require at least eight characters containing letters and digits. Five failed sign-in attempts cause a 15-minute lockout. Authentication uses an eight-hour cookie with sliding expiration. Password reset is administrator-assisted; self-service recovery is deferred.

Each staff account has exactly one role in V1. Multiple roles per account are deferred.
