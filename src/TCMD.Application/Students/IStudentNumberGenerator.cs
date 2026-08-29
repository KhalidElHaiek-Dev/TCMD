namespace TCMD.Application.Students;

public interface IStudentNumberGenerator
{
    Task<string> NextAsync(CancellationToken cancellationToken);
}
