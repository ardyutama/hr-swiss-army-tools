using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;

namespace hr_sat.Application.Features.Vacancies;

public sealed record GetVacancyQuery(long Id) : IQuery<VacancyDetailsResponse>;
