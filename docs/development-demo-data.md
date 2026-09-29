# Development demo data

TCMD includes an explicit, one-shot command that fills a dedicated local database with fictional portfolio data. It is intended only for screenshots, interviews, and local demonstrations.

## Safety boundaries

- The command works only when `ASPNETCORE_ENVIRONMENT=Development`.
- The connection string must name the database exactly `TCMD.Demo`.
- The command migrates that database, but never deletes or overwrites records.
- Seeding refuses to run if any TCMD domain or staff-account data already exists.
- Normal application startup never invokes the demo seeder. `TCMD.Development` is not an accepted target.

Review the resolved connection string before running the command. Never point `ConnectionStrings__TCMD` at a shared or real database.

## Run the seed

PowerShell example using LocalDB:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__TCMD = 'Server=(localdb)\MSSQLLocalDB;Database=TCMD.Demo;Trusted_Connection=True;TrustServerCertificate=True'
dotnet run --project src/TCMD.Api -- --seed-demo
```

The process prints the target warning, applies EF Core migrations, creates the data, and exits without starting the web server. Remove the two environment variables from the shell when finished.

To run the application against the populated database, set the same environment and connection string and run without `--seed-demo`.

## Development-only credentials

All three accounts use the password `TcmdDemo2026`. These credentials exist only in the explicitly seeded `TCMD.Demo` database and must never be reused elsewhere.

| Username | Role | Detail |
| --- | --- | --- |
| `admin.demo` | Administrator | Full demo and staff-account access |
| `staff.demo` | Staff | Operational workflow access |
| `instructor.demo` | Instructor | Linked to Youssef Benali |

## Screenshot candidates

- Dashboard: sign in as `admin.demo`; “Project Coordination — Next Intake” is the single Planned group requiring an Instructor.
- Students List and Student Detail: use Amira El Mansouri (`STU-000001`), who has Active and Completed enrollment history plus attendance.
- Training Group Detail: use “Digital Operations — Autumn Cohort” for an active roster, completed and future sessions, attendance, and a cancelled session.
- Session Attendance: use the first completed “Digital Operations — Autumn Cohort” session. It has Present, Absent, Late, Excused, and one Not Recorded roster member.
- Staff Account Detail: use `instructor.demo`, linked to the active Instructor Youssef Benali.
- Login: use any development-only account above.

The completed “Excel Foundations — Spring Cohort” provides historical enrollment and attendance. “Customer Service — Summer Cohort” demonstrates a Cancelled group. “Workplace Communication — Winter Cohort” is a staffed Planned group with a future Scheduled session.

## Reseeding and reset

Running the seed command again refuses safely and creates no duplicates. There is intentionally no automatic reset or delete command. To start over, manually drop only the explicitly designated local `TCMD.Demo` database using your normal SQL Server tooling, verify the database name first, and then rerun the seed command. EF migrations recreate the schema.

Missing attendance is represented by no `AttendanceRecord`, which is TCMD's domain definition of **Not Recorded**. Demo records are created through domain factories and valid lifecycle transitions; no production rules or schema constraints are bypassed.
