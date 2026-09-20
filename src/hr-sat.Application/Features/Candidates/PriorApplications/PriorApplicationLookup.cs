using hr_sat.Application.Abstractions.Data;
using hr_sat.Domain.Candidates.FormResponses;
using Microsoft.EntityFrameworkCore;

namespace hr_sat.Application.Features.Candidates.PriorApplications;

internal static class PriorApplicationLookup
{
    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<PriorApplicationMatch>>> FindAsync(
        long vacancyId,
        IReadOnlySet<string> emailKeys,
        IApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var matches = new Dictionary<string, IReadOnlyList<PriorApplicationMatch>>(
            StringComparer.Ordinal);
        var keys = emailKeys
            .Where(CandidateFormIdentity.IsEmailKey)
            .ToHashSet(StringComparer.Ordinal);
        if (keys.Count == 0)
        {
            return matches;
        }

        var senderMatches = await dbContext.Candidates
            .AsNoTracking()
            .Join(
                dbContext.IntakeRounds.AsNoTracking(),
                candidate => candidate.IntakeRoundId,
                round => round.Id,
                (candidate, round) => new { Candidate = candidate, Round = round })
            .Where(prior =>
                prior.Round.VacancyId == vacancyId &&
                prior.Candidate.SourceSenderEmail != null &&
                keys.Contains(prior.Candidate.SourceSenderEmail!.Trim().ToLower()))
            .Select(prior => new
            {
                Key = prior.Candidate.SourceSenderEmail!.Trim().ToLower(),
                CandidateId = prior.Candidate.Id,
                prior.Round.RoundNumber,
                RoundName = prior.Round.Name,
                ReviewStatus = prior.Candidate.ReviewStatus.ToString().ToLowerInvariant(),
                prior.Candidate.ImportedAt
            })
            .ToListAsync(cancellationToken);

        var formMatches = await dbContext.CandidateFormResponses
            .AsNoTracking()
            .Join(
                dbContext.Candidates.AsNoTracking(),
                response => response.CandidateId,
                candidate => candidate.Id,
                (response, candidate) => new { Response = response, Candidate = candidate })
            .Join(
                dbContext.IntakeRounds.AsNoTracking(),
                prior => prior.Candidate.IntakeRoundId,
                round => round.Id,
                (prior, round) => new
                {
                    prior.Response,
                    prior.Candidate,
                    Round = round
                })
            .Where(prior =>
                prior.Round.VacancyId == vacancyId &&
                prior.Response.IsCurrent &&
                prior.Response.IdentityKey != null &&
                keys.Contains(prior.Response.IdentityKey!))
            .Select(prior => new
            {
                Key = prior.Response.IdentityKey!,
                CandidateId = prior.Candidate.Id,
                prior.Round.RoundNumber,
                RoundName = prior.Round.Name,
                ReviewStatus = prior.Candidate.ReviewStatus.ToString().ToLowerInvariant(),
                prior.Candidate.ImportedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var keyGroup in senderMatches
            .Concat(formMatches)
            .GroupBy(match => match.Key, StringComparer.Ordinal))
        {
            matches[keyGroup.Key] = keyGroup
                .GroupBy(match => match.CandidateId)
                .Select(candidateGroup => candidateGroup.First())
                .OrderByDescending(match => match.RoundNumber)
                .ThenByDescending(match => match.ImportedAt)
                .ThenByDescending(match => match.CandidateId)
                .Select(match => new PriorApplicationMatch(
                    match.CandidateId,
                    match.RoundNumber,
                    match.RoundName,
                    match.ReviewStatus,
                    match.ImportedAt))
                .ToList();
        }

        return matches;
    }
}

internal sealed record PriorApplicationMatch(
    long CandidateId,
    int RoundNumber,
    string? RoundName,
    string ReviewStatus,
    DateTimeOffset ImportedAt);
