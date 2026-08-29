using Microsoft.EntityFrameworkCore;
using TCMD.Domain.Students;

namespace TCMD.Infrastructure.Persistence;

public sealed class TcmdDbContext(DbContextOptions<TcmdDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("StudentNumberSequence", "dbo")
            .StartsAt(1)
            .IncrementsBy(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TcmdDbContext).Assembly);
    }
}
