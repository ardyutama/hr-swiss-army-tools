using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Abstractions.Storage;
using hr_sat.Application.Features.Candidates.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.Extract;

internal sealed class ExtractCandidateCommandHandler(
    IApplicationDbContext dbContext,
    IPrivateFileStorage fileStorage,
    CandidateCvExtractionService extractionService)
    : ICommandHandler<ExtractCandidateCommand, CandidateDetailsResponse>
{
    public async Task<Result<CandidateDetailsResponse>> Handle(
        ExtractCandidateCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        var vacancy = await dbContext.FindVacancyForUpdateAsync(
            command.VacancyId,
            cancellationToken);
        if (vacancy is null)
        {
            return Result<CandidateDetailsResponse>.Failure(
                CandidateErrors.NotFound(command.VacancyId));
        }

        var canModifyResult = vacancy.EnsureCanModifyCandidate();
        if (canModifyResult.IsFailure)
        {
            return Result<CandidateDetailsResponse>.Failure(canModifyResult.Error);
        }

        var candidate = await dbContext.Candidates
            .Include(item => item.CvDocuments)
            .Include(item => item.Skills)
            .SingleOrDefaultAsync(
                item => item.Id == command.CandidateId && item.VacancyId == command.VacancyId,
                cancellationToken);
        if (candidate is null)
        {
            return Result<CandidateDetailsResponse>.Failure(
                CandidateErrors.NotFound(command.CandidateId));
        }

        var primaryDocument = candidate.CvDocuments.SingleOrDefault(document => document.IsPrimary);
        if (primaryDocument is null)
        {
            candidate.MarkExtractionPending();
        }
        else
        {
            await extractionService.TryApplyAsync(
                candidate,
                fileStorage,
                primaryDocument.StorageKey,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CandidateDetailsResponse.From(
            command.VacancyId,
            candidate,
            vacancy.Requirements.Select(requirement => requirement.Phrase));
    }
}
