using hr_sat.Domain.Candidates;
using hr_sat.Domain.Vacancies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace hr_sat.Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<Vacancy> Vacancies { get; }
    DbSet<VacancyRequirement> VacancyRequirements { get; }
    DbSet<Candidate> Candidates { get; }
    DbSet<CvDocument> CvDocuments { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);

    Task<Vacancy?> FindVacancyForUpdateAsync(long id, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}