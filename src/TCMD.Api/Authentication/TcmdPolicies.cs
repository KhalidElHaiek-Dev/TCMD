namespace TCMD.Api.Authentication;

public static class TcmdPolicies
{
    public const string OperationalStaff = "OperationalStaff";
    public const string Administrators = "Administrators";
    public const string OperationalStaffOrInstructor = "OperationalStaffOrInstructor";
    public static readonly string[] Roles = ["Administrator", "Staff", "Instructor"];
}
