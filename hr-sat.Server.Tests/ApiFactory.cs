using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace hr_sat.Server.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());
    }

    public new HttpClient CreateClient()
    {
        return CreateDefaultClient(new DatabaseResetHandler(ResetDatabaseAsync));
    }

    private async Task ResetDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "TRUNCATE TABLE vacancy RESTART IDENTITY CASCADE";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    private sealed class DatabaseResetHandler(
        Func<CancellationToken, Task> resetDatabaseAsync) : DelegatingHandler
    {
        private int _hasReset;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _hasReset, 1) == 0)
            {
                await resetDatabaseAsync(cancellationToken);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
