using hr_sat.Domain;

namespace hr_sat.Domain.Vacancies;

public sealed class VacancyRequirement : Entity
{
    private VacancyRequirement()
    {
    }

    internal VacancyRequirement(string phrase, int position)
    {
        Phrase = phrase;
        Position = position;
    }

    public long VacancyId { get; private set; }
    public string Phrase { get; private set; } = string.Empty;
    public string PhraseNormalized { get; private set; } = string.Empty;
    public int Position { get; private set; }
}