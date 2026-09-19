using hr_sat.Domain.Candidates;
using hr_sat.Domain.EmailTemplates;
using hr_sat.Domain.IntakeRounds;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Candidates.FormResponses;
using hr_sat.Domain.Vacancies.FormLayouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace hr_sat.Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<Vacancy> Vacancies { get; }
    DbSet<VacancyRequirement> VacancyRequirements { get; }
    DbSet<Candidate> Candidates { get; }
    DbSet<CandidateFormResponse> CandidateFormResponses { get; }
    DbSet<IntakeRound> IntakeRounds { get; }
    DbSet<CandidateRequirementReview> CandidateRequirementReviews { get; }
    DbSet<CvDocument> CvDocuments { get; }
    DbSet<PendingFileDeletion> PendingFileDeletions { get; }
    DbSet<EmailTemplate> EmailTemplates { get; }
    DbSet<FormLayout> FormLayouts { get; }
    DbSet<ScreeningRuleSet> ScreeningRuleSets { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);

    Task<Vacancy?> FindVacancyForUpdateAsync(long id, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}