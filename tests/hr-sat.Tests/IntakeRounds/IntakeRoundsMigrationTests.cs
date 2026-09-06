using hr_sat.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace hr_sat.Tests.IntakeRounds;

public sealed class IntakeRoundsMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .Build();

    public Task InitializeAsync() => postgres.StartAsync();

    public async Task DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task Up_and_down_should_preserve_legacy_candidates_and_enforce_round_constraints()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using var dbContext = new AppDbContext(options);

        await dbContext.Database.MigrateAsync(
            "20260905070727_ReviewWorkspace_V1",
            CancellationToken.None);
        var (vacancyId, candidateId) = await SeedLegacyVacancyAndCandidateAsync();

        await dbContext.Database.MigrateAsync(
            "20260906154337_Add_Intake_Rounds",
            CancellationToken.None);

        var backfill = await ReadBackfilledCandidateAsync(vacancyId, candidateId);
        Assert.Equal(1, backfill.RoundNumber);
        Assert.Equal(backfill.RoundId, backfill.CandidateRoundId);

        await AssertActiveRoundIndexAsync(vacancyId, backfill.RoundId);
        await AssertIdentitySequencesAsync(vacancyId, candidateId);

        await dbContext.Database.MigrateAsync(
            "20260905070727_ReviewWorkspace_V1",
            CancellationToken.None);

        var restoredVacancyId = await ReadLegacyCandidateVacancyIdAsync(candidateId);
        Assert.Equal(vacancyId, restoredVacancyId);
        Assert.True(await TableExistsAsync("intake_round") is false);
        Assert.True(await ColumnExistsAsync("candidate", "vacancy_id"));
        Assert.False(await ColumnExistsAsync("candidate", "intake_round_id"));
    }

    private async Task<(long VacancyId, long CandidateId)> SeedLegacyVacancyAndCandidateAsync()
    {
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH inserted_vacancy AS (
                INSERT INTO vacancy (title, opened_on, status, closed_at)
                VALUES ('Legacy Vacancy', DATE '2026-09-01', 'open', NULL)
                RETURNING id
            )
            INSERT INTO candidate (
                vacancy_id,
                source_original_filename,
                source_storage_key,
                source_size_bytes,
                source_sha256
            )
            SELECT
                id,
                'legacy-candidate.eml',
                'legacy/legacy-candidate.eml',
                1,
                decode(repeat('ab', 32), 'hex')
            FROM inserted_vacancy
            RETURNING vacancy_id, id;
            """;

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private async Task<(long RoundId, int RoundNumber, long CandidateRoundId)> ReadBackfilledCandidateAsync(
        long vacancyId,
        long candidateId)
    {
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT round.id, round.round_number, candidate.intake_round_id
            FROM intake_round AS round
            JOIN candidate ON candidate.intake_round_id = round.id
            WHERE round.vacancy_id = $1
              AND round.round_number = 1
              AND candidate.id = $2;
            """;
        command.Parameters.AddWithValue(vacancyId);
        command.Parameters.AddWithValue(candidateId);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt64(0), reader.GetInt32(1), reader.GetInt64(2));
    }

    private async Task AssertActiveRoundIndexAsync(long vacancyId, long defaultRoundId)
    {
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();

        await using (var closedRoundCommand = connection.CreateCommand())
        {
            closedRoundCommand.CommandText = """
                INSERT INTO intake_round (vacancy_id, round_number, name, closed_at)
                VALUES ($1, 2, 'Closed legacy round', now());
                """;
            closedRoundCommand.Parameters.AddWithValue(vacancyId);
            await closedRoundCommand.ExecuteNonQueryAsync();
        }

        await using var duplicateOpenRoundCommand = connection.CreateCommand();
        duplicateOpenRoundCommand.CommandText = """
            INSERT INTO intake_round (vacancy_id, round_number, name, closed_at)
            VALUES ($1, 3, 'Duplicate active round', NULL);
            """;
        duplicateOpenRoundCommand.Parameters.AddWithValue(vacancyId);
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => duplicateOpenRoundCommand.ExecuteNonQueryAsync());
        Assert.Equal("23505", exception.SqlState);
        Assert.True(defaultRoundId > 0);
    }

    private async Task AssertIdentitySequencesAsync(long legacyVacancyId, long legacyCandidateId)
    {
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        long newVacancyId;
        long newRoundId;
        long newCandidateId;

        await using (var vacancyCommand = connection.CreateCommand())
        {
            vacancyCommand.CommandText = """
                INSERT INTO vacancy (title, opened_on)
                VALUES ('Sequence Vacancy', DATE '2026-09-02')
                RETURNING id;
                """;
            newVacancyId = (long)(await vacancyCommand.ExecuteScalarAsync())!;
        }

        await using (var roundCommand = connection.CreateCommand())
        {
            roundCommand.CommandText = """
                INSERT INTO intake_round (vacancy_id, round_number, name, closed_at)
                VALUES ($1, 1, NULL, NULL)
                RETURNING id;
                """;
            roundCommand.Parameters.AddWithValue(newVacancyId);
            newRoundId = (long)(await roundCommand.ExecuteScalarAsync())!;
        }

        await using (var candidateCommand = connection.CreateCommand())
        {
            candidateCommand.CommandText = """
                INSERT INTO candidate (
                    intake_round_id,
                    source_original_filename,
                    source_storage_key,
                    source_size_bytes,
                    source_sha256
                )
                VALUES ($1, 'sequence-candidate.eml', 'sequence/candidate.eml', 1, decode(repeat('cd', 32), 'hex'))
                RETURNING id;
                """;
            candidateCommand.Parameters.AddWithValue(newRoundId);
            newCandidateId = (long)(await candidateCommand.ExecuteScalarAsync())!;
        }

        Assert.True(newVacancyId > legacyVacancyId);
        Assert.True(newCandidateId > legacyCandidateId);
    }

    private async Task<long> ReadLegacyCandidateVacancyIdAsync(long candidateId)
    {
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT vacancy_id FROM candidate WHERE id = $1;";
        command.Parameters.AddWithValue(candidateId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<bool> TableExistsAsync(string tableName)
    {
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT to_regclass('public.' || $1) IS NOT NULL;
            """;
        command.Parameters.AddWithValue(tableName);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private async Task<bool> ColumnExistsAsync(string tableName, string columnName)
    {
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = $1
                  AND column_name = $2);
            """;
        command.Parameters.AddWithValue(tableName);
        command.Parameters.AddWithValue(columnName);
        return (bool)(await command.ExecuteScalarAsync())!;
    }
}
