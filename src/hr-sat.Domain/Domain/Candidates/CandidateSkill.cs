using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

public sealed class CandidateSkill : Entity
{
    private CandidateSkill()
    {
    }

    internal CandidateSkill(string phrase, int position)
    {
        Phrase = phrase;
        Position = position;
    }

    public long CandidateId { get; private set; }
    public string Phrase { get; private set; } = string.Empty;
    public string PhraseNormalized { get; private set; } = string.Empty;
    public int Position { get; private set; }
}
