using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TCMD.Application.Courses;
using TCMD.Domain.Courses;
using TCMD.Infrastructure.Persistence;

namespace TCMD.Infrastructure.Courses;

internal sealed class EfCourseStore(TcmdDbContext dbContext) : ICourseStore
{
    public async Task<CourseStoreSaveStatus> AddAsync(Course course, CancellationToken cancellationToken)
    {
        dbContext.Courses.Add(course);
        return await SaveChangesAsync(course, cancellationToken);
    }

    public Task<Course?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Courses.AsNoTracking().SingleOrDefaultAsync(course => course.Id == id, cancellationToken);

    public Task<Course?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Courses.SingleOrDefaultAsync(course => course.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Course>> SearchAsync(string? search, bool? isActive, CancellationToken cancellationToken)
    {
        var query = dbContext.Courses.AsNoTracking();
        if (isActive is not null) query = query.Where(course => course.IsActive == isActive);
        if (search is not null)
            query = query.Where(course => course.Code.Contains(search) || course.Name.Contains(search) ||
                (course.Description != null && course.Description.Contains(search)));
        return await query.OrderBy(course => course.Code).ThenBy(course => course.Id).ToListAsync(cancellationToken);
    }

    public async Task<CourseStoreSaveStatus> SaveAsync(Course course, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        dbContext.Entry(course).Property(candidate => candidate.RowVersion).OriginalValue = expectedRowVersion;
        return await SaveChangesAsync(course, cancellationToken);
    }

    private async Task<CourseStoreSaveStatus> SaveChangesAsync(Course course, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return CourseStoreSaveStatus.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.Entry(course).State = EntityState.Detached;
            return CourseStoreSaveStatus.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            dbContext.Entry(course).State = EntityState.Detached;
            return CourseStoreSaveStatus.DuplicateCode;
        }
    }
}
