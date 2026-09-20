using hr_sat.Application.Abstractions.Dispatching;
using Npgsql;

namespace hr_sat.Infrastructure.Dispatching;

// Session-level advisory lock on a dedicated connection: dispatch rows commit
// append-as-you-go on the request's own context, so the lock cannot ride a transaction
// there. Disposing the handle closes the session, which releases the lock.
public sealed class PostgresDispatchRunLock(string connectionString) : IDispatchRunLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(
        long vacancyId,
        long roundId,
        CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                "SELECT pg_try_advisory_lock(hashtext(@key))",
                connection);
            command.Parameters.AddWithValue("key", $"dispatch:{vacancyId}:{roundId}");
            var acquired = (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
            if (!acquired)
            {
                await connection.DisposeAsync();
                return null;
            }

            return new Handle(connection);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class Handle(NpgsqlConnection connection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }
    }
}
