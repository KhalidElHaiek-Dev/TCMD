using Microsoft.AspNetCore.Identity;

namespace TCMD.Infrastructure.Identity;

public sealed class StaffUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid? InstructorId { get; set; }
}
