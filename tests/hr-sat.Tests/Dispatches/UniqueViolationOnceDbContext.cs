using hr_sat.Tests.Candidates;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace hr_sat.Tests.Dispatches;

// Arms a one-shot 23505-wrapped DbUpdateException so dispatch handler tests can
// exercise the unique-violation catch (the filtered dispatch_candidate_sent_key
// backstop) without a real Postgres race.
internal sealed class UniqueViolationOnceDbContext : TestDbContext
{
    private int savesUntilViolation = -1;

    public void ThrowUniqueViolationAfterSaves(int saves) => savesUntilViolation = saves;

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (savesUntilViolation == 0)
        {
            savesUntilViolation = -1;
            throw new DbUpdateException(
                "Insert failed.",
                new PostgresException(
                    "duplicate key value violates unique constraint \"dispatch_candidate_sent_key\"",
                    "ERROR",
                    "ERROR",
                    PostgresErrorCodes.UniqueViolation));
        }

        if (savesUntilViolation > 0)
        {
            savesUntilViolation--;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
