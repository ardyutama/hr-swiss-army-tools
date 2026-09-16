using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

public sealed class CandidateFormResponse : Entity
{
    private CandidateFormResponse()
    {
    }

    private CandidateFormResponse(CandidateFormResponseData responseData)
    {
        Cells = responseData.Cells.ToArray();
        FormTimestampRaw = responseData.FormTimestampRaw;
        FormTimestampParsed = responseData.FormTimestampParsed;
        IdentityKey = responseData.IdentityKey;
        IsCurrent = true;
        ImportedAt = responseData.ImportedAt;
    }

    public long CandidateId { get; private set; }
    public string[] Cells { get; private set; } = [];
    public string FormTimestampRaw { get; private set; } = string.Empty;
    public DateTimeOffset? FormTimestampParsed { get; private set; }
    public string? IdentityKey { get; private set; }
    public bool IsCurrent { get; private set; }
    public DateTimeOffset ImportedAt { get; private set; }

    internal static Result<CandidateFormResponse> Create(CandidateFormResponseData responseData)
    {
        if (responseData.Cells.Count == 0)
        {
            return Result<CandidateFormResponse>.Failure(CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["cells"] = ["A form response must contain at least one cell."]
            }));
        }

        if (responseData.ImportedAt == default)
        {
            return Result<CandidateFormResponse>.Failure(CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["importedAt"] = ["The form response import timestamp is required."]
            }));
        }

        return new CandidateFormResponse(responseData);
    }

    internal void MarkPrior()
    {
        IsCurrent = false;
    }
}