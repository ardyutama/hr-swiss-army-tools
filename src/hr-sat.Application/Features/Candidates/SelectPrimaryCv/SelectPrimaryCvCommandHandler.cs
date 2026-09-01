using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Application.Features.Candidates.Shared;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.SelectPrimaryCv;

internal sealed class SelectPrimaryCvCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<SelectPrimaryCvCommand, CandidateDetailsResponse>
{
    public async Task<Result<CandidateDetailsResponse>> Handle(
        SelectPrimaryCvCommand command,
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

        var selectResult = candidate.SelectPrimaryCv(command.DocumentId);
        if (selectResult.IsFailure)
        {
            return Result<CandidateDetailsResponse>.Failure(selectResult.Error);
        }

        await dbContext.CvDocuments
            .Where(document =>
                document.CandidateId == command.CandidateId &&
                document.Id != command.DocumentId &&
                document.IsPrimary)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(document => document.IsPrimary, false),
                cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return CandidateDetailsResponse.From(
            command.VacancyId,
            candidate,
            vacancy.Requirements.Select(requirement => requirement.Phrase));
    }
}
