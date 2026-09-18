using hr_sat.Application.Features.Vacancies;
using hr_sat.Tests.Candidates;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace hr_sat.Tests.Vacancies;

public sealed class PurgeVacancyHandlerTests
{
    [Fact]
    public async Task Handle_Should_DeleteVacancyAndEnqueueFileDeletions_WhenVacancyExists() // domain: purge removes a vacancy and queues its owned files for deletion
    {
        await using var dbContext = new TestDbContext();
        var (vacancy, candidate) = await CandidateTestData.SeedCandidateAsync(dbContext);
        var document = candidate.CvDocuments.Single();
        var handler = new PurgeVacancyCommandHandler(
            dbContext,
            new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));

        var result = await handler.Handle(
            new PurgeVacancyCommand(vacancy.Id),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        (await dbContext.Vacancies.AnyAsync()).ShouldBeFalse();
        (await dbContext.Candidates.AnyAsync()).ShouldBeFalse();
        (await dbContext.CvDocuments.AnyAsync()).ShouldBeFalse();
        var pendingKeys = await dbContext.PendingFileDeletions
            .Select(deletion => deletion.StorageKey)
            .ToListAsync();
        pendingKeys.ShouldBe([candidate.SourceStorageKey, document.StorageKey], ignoreOrder: true);
    }
}