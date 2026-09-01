using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace hr_sat.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .Build();
    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(),
        "hr-sat-tests",
        Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());
        builder.UseSetting("FileStorage:RootPath", _storageRoot);
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
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
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
