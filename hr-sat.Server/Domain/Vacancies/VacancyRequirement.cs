namespace hr_sat.Server.Domain.Vacancies;

public sealed class VacancyRequirement
{
    private VacancyRequirement()
    {
    }

    internal VacancyRequirement(string phrase, int position)
    {
        Phrase = phrase;
        Position = position;
    }

    public long Id { get; private set; }
    public long VacancyId { get; private set; }
    public string Phrase { get; private set; } = string.Empty;
    public string PhraseNormalized { get; private set; } = string.Empty;
    public int Position { get; private set; }
}