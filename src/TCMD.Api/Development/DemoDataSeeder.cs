using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TCMD.Api.Authentication;
using TCMD.Domain.Attendance;
using TCMD.Domain.Courses;
using TCMD.Domain.Enrollments;
using TCMD.Domain.Instructors;
using TCMD.Domain.Students;
using TCMD.Domain.TrainingGroups;
using TCMD.Domain.TrainingSessions;
using TCMD.Infrastructure.Identity;
using TCMD.Infrastructure.Persistence;

namespace TCMD.Api.Development;

public sealed class DemoDataSeeder(TcmdDbContext db, UserManager<StaffUser> users,
    RoleManager<IdentityRole<Guid>> roles, TimeProvider timeProvider)
{
    public const string AdministratorUserName = "admin.demo";
    public const string StaffUserName = "staff.demo";
    public const string InstructorUserName = "instructor.demo";
    public const string DemoPassword = "TcmdDemo2026";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await SeedCoreAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task SeedCoreAsync(CancellationToken cancellationToken)
    {
        if (await HasExistingDataAsync(cancellationToken))
            throw new InvalidOperationException("Demo seeding refused because the database is not empty. No data was changed.");

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var instructors = CreateInstructors(now);
        var courses = CreateCourses(now);
        var students = CreateStudents(now);
        db.AddRange(instructors.Values);
        db.AddRange(courses.Values);
        db.AddRange(students.Values);
        await db.SaveChangesAsync(cancellationToken);

        var groups = CreateGroups(courses, instructors, today, now);
        db.AddRange(groups.Values);
        await db.SaveChangesAsync(cancellationToken);

        var enrollments = CreateEnrollments(students, groups, today, now);
        db.AddRange(enrollments.Values);
        var sessions = CreateSessions(groups, today, now);
        db.AddRange(sessions.Values);
        await db.SaveChangesAsync(cancellationToken);

        var admin = await CreateAccountsAsync(instructors["Youssef"].Id, cancellationToken);
        db.AddRange(CreateAttendance(enrollments, sessions, admin.Id, now));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> HasExistingDataAsync(CancellationToken token) =>
        await db.Users.AnyAsync(token) || await db.Students.AnyAsync(token) || await db.Instructors.AnyAsync(token) ||
        await db.Courses.AnyAsync(token) || await db.TrainingGroups.AnyAsync(token) ||
        await db.Enrollments.AnyAsync(token) || await db.TrainingSessions.AnyAsync(token) ||
        await db.AttendanceRecords.AnyAsync(token);

    private static Dictionary<string, Student> CreateStudents(DateTimeOffset now)
    {
        string[] names = ["Amira El Mansouri", "Karim Haddad", "Salma Idrissi", "Omar Naciri", "Lina Farouk",
            "Adam Cherkaoui", "Hana Berrada", "Yasmine Tazi", "Rayan Alaoui", "Sofia Amrani"];
        return names.Select((name, index) => Student.Register($"STU-{index + 1:000000}", name,
                $"+212 600 000 {index + 1:000}", $"{name.ToLowerInvariant().Replace(' ', '.')}@example.test", now))
            .ToDictionary(student => student.FullName.Split(' ')[0], StringComparer.Ordinal);
    }

    private static Dictionary<string, Instructor> CreateInstructors(DateTimeOffset now) => new()
    {
        ["Youssef"] = Instructor.Create("Youssef Benali", "+212 600 100 001", "youssef.benali@example.test", now),
        ["Nadia"] = Instructor.Create("Nadia Rahal", "+212 600 100 002", "nadia.rahal@example.test", now),
        ["Samir"] = Instructor.Create("Samir El Fassi", "+212 600 100 003", "samir.elfassi@example.test", now),
        ["Leila"] = Instructor.Create("Leila Mansour", "+212 600 100 004", "leila.mansour@example.test", now)
    };

    private static Dictionary<string, Course> CreateCourses(DateTimeOffset now) => new()
    {
        ["DGO"] = Course.Create("DGO-101", "Digital Operations", "Practical digital workflows for modern operations teams.", now),
        ["COM"] = Course.Create("COM-110", "Workplace Communication", "Clear, confident communication in professional settings.", now),
        ["XLS"] = Course.Create("XLS-120", "Excel Fundamentals", "Core spreadsheet skills for organizing and analyzing work data.", now),
        ["CSE"] = Course.Create("CSE-130", "Customer Service Essentials", "Service techniques for consistent customer experiences.", now),
        ["PRJ"] = Course.Create("PRJ-140", "Project Coordination", "Foundations for planning, tracking, and coordinating projects.", now)
    };

    private static Dictionary<string, TrainingGroup> CreateGroups(Dictionary<string, Course> c,
        Dictionary<string, Instructor> i, DateOnly today, DateTimeOffset now)
    {
        var active = TrainingGroup.Create("Digital Operations — Autumn Cohort", c["DGO"].Id, i["Youssef"].Id, today.AddDays(-21), today.AddDays(35), now); active.Activate(now);
        var completed = TrainingGroup.Create("Excel Foundations — Spring Cohort", c["XLS"].Id, i["Nadia"].Id, today.AddDays(-120), today.AddDays(-70), now); completed.Activate(now); completed.Complete(now);
        var plannedAttention = TrainingGroup.Create("Project Coordination — Next Intake", c["PRJ"].Id, null, today.AddDays(30), today.AddDays(75), now);
        var planned = TrainingGroup.Create("Workplace Communication — Winter Cohort", c["COM"].Id, i["Leila"].Id, today.AddDays(50), today.AddDays(85), now);
        var cancelled = TrainingGroup.Create("Customer Service — Summer Cohort", c["CSE"].Id, i["Samir"].Id, today.AddDays(-60), today.AddDays(-25), now); cancelled.Cancel(now);
        return new() { ["Active"] = active, ["Completed"] = completed, ["Attention"] = plannedAttention, ["Planned"] = planned, ["Cancelled"] = cancelled };
    }

    private static Dictionary<string, Enrollment> CreateEnrollments(Dictionary<string, Student> s,
        Dictionary<string, TrainingGroup> g, DateOnly today, DateTimeOffset now)
    {
        var result = new Dictionary<string, Enrollment>();
        void Add(string key, string student, string group, int daysAgo, string status = "Active")
        {
            var item = Enrollment.Create(s[student].Id, g[group].Id, today.AddDays(-daysAgo), now);
            if (status == "Completed") item.Complete(now); else if (status == "Withdrawn") item.Withdraw(now);
            result[key] = item;
        }
        Add("AmiraActive", "Amira", "Active", 30); Add("KarimActive", "Karim", "Active", 28);
        Add("SalmaActive", "Salma", "Active", 27); Add("OmarActive", "Omar", "Active", 26);
        Add("LinaActive", "Lina", "Active", 25); Add("RayanActive", "Rayan", "Active", 24);
        Add("AmiraCompleted", "Amira", "Completed", 150, "Completed"); Add("HanaCompleted", "Hana", "Completed", 145, "Completed");
        Add("YasmineCompleted", "Yasmine", "Completed", 140, "Completed"); Add("SofiaCompleted", "Sofia", "Completed", 135, "Completed");
        Add("AdamWithdrawn", "Adam", "Active", 23, "Withdrawn"); Add("KarimPlanned", "Karim", "Planned", 2);
        Add("SalmaAttention", "Salma", "Attention", 1);
        return result;
    }

    private static Dictionary<string, TrainingSession> CreateSessions(Dictionary<string, TrainingGroup> g,
        DateOnly today, DateTimeOffset now)
    {
        TrainingSession Session(string group, int offset, int hour, string room) => TrainingSession.Create(g[group].Id, today.AddDays(offset), new(hour, 0), new(hour + 2, 0), room, now);
        var a1 = Session("Active", -14, 9, "Learning Lab A"); a1.Complete(now);
        var a2 = Session("Active", -7, 9, "Learning Lab A"); a2.Complete(now);
        var a3 = Session("Active", 7, 9, "Learning Lab A");
        var a4 = Session("Active", 14, 9, "Learning Lab A"); a4.Cancel(now);
        var c1 = Session("Completed", -110, 14, "Computer Room 2"); c1.Complete(now);
        var c2 = Session("Completed", -100, 14, "Computer Room 2"); c2.Complete(now);
        var c3 = Session("Completed", -90, 14, "Computer Room 2"); c3.Complete(now);
        var p1 = Session("Planned", 55, 10, "Seminar Room 1");
        return new() { ["ActiveOne"] = a1, ["ActiveTwo"] = a2, ["ActiveFuture"] = a3, ["ActiveCancelled"] = a4,
            ["CompletedOne"] = c1, ["CompletedTwo"] = c2, ["CompletedThree"] = c3, ["PlannedFuture"] = p1 };
    }

    private static IEnumerable<AttendanceRecord> CreateAttendance(Dictionary<string, Enrollment> e,
        Dictionary<string, TrainingSession> s, Guid actor, DateTimeOffset now)
    {
        AttendanceRecord Make(string enrollment, string session, AttendanceStatus status, string? note = null)
        {
            var record = AttendanceRecord.Create(e[enrollment].Id, s[session].Id, status, actor, now);
            if (note is not null) record.Correct(status, note, actor, now);
            return record;
        }
        return [
            Make("AmiraActive", "ActiveOne", AttendanceStatus.Present), Make("KarimActive", "ActiveOne", AttendanceStatus.Late, "Arrival time corrected after register review."),
            Make("SalmaActive", "ActiveOne", AttendanceStatus.Absent), Make("OmarActive", "ActiveOne", AttendanceStatus.Excused), Make("LinaActive", "ActiveOne", AttendanceStatus.Present),
            Make("AmiraActive", "ActiveTwo", AttendanceStatus.Late), Make("KarimActive", "ActiveTwo", AttendanceStatus.Present), Make("SalmaActive", "ActiveTwo", AttendanceStatus.Present),
            Make("OmarActive", "ActiveTwo", AttendanceStatus.Absent, "Status corrected from present after facilitator confirmation."),
            Make("AmiraCompleted", "CompletedOne", AttendanceStatus.Present), Make("HanaCompleted", "CompletedOne", AttendanceStatus.Present),
            Make("YasmineCompleted", "CompletedOne", AttendanceStatus.Late), Make("SofiaCompleted", "CompletedOne", AttendanceStatus.Excused),
            Make("AmiraCompleted", "CompletedTwo", AttendanceStatus.Present), Make("HanaCompleted", "CompletedTwo", AttendanceStatus.Absent),
            Make("YasmineCompleted", "CompletedTwo", AttendanceStatus.Present), Make("SofiaCompleted", "CompletedTwo", AttendanceStatus.Present)
        ];
    }

    private async Task<StaffUser> CreateAccountsAsync(Guid instructorId, CancellationToken token)
    {
        foreach (var role in TcmdPolicies.Roles)
            if (!await roles.RoleExistsAsync(role) && !(await roles.CreateAsync(new IdentityRole<Guid>(role))).Succeeded)
                throw new InvalidOperationException($"Could not create demo role '{role}'.");
        var admin = await CreateUserAsync(AdministratorUserName, "Demo Administrator", "Administrator", token);
        await CreateUserAsync(StaffUserName, "Demo Staff Member", "Staff", token);
        var instructor = await CreateUserAsync(InstructorUserName, "Youssef Benali", "Instructor", token);
        instructor.InstructorId = instructorId;
        if (!(await users.UpdateAsync(instructor)).Succeeded) throw new InvalidOperationException("Could not link the demo Instructor account.");
        return admin;
    }

    private async Task<StaffUser> CreateUserAsync(string userName, string displayName, string role, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var user = new StaffUser { UserName = userName, DisplayName = displayName, IsActive = true };
        var created = await users.CreateAsync(user, DemoPassword);
        if (!created.Succeeded) throw new InvalidOperationException($"Could not create demo account '{userName}': {string.Join(", ", created.Errors.Select(x => x.Description))}");
        if (!(await users.AddToRoleAsync(user, role)).Succeeded) throw new InvalidOperationException($"Could not assign demo role '{role}'.");
        return user;
    }
}
