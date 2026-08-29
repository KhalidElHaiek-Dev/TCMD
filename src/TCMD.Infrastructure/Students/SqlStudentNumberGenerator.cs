using System.Data;
using Microsoft.EntityFrameworkCore;
using TCMD.Application.Students;
using TCMD.Infrastructure.Persistence;

namespace TCMD.Infrastructure.Students;

internal sealed class SqlStudentNumberGenerator(TcmdDbContext dbContext) : IStudentNumberGenerator
{
    public async Task<string> NextAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT NEXT VALUE FOR dbo.StudentNumberSequence";
            var value = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            return $"STU-{value:000000}";
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
