using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

public sealed class Candidate : Entity
{
    private readonly List<CvDocument> _cvDocuments = [];
    private readonly List<CandidateRequirementReview> _requirementReviews = [];
    private readonly List<CandidateFormResponse> _formResponses = [];

    private Candidate()
    {
    }

    private Candidate(
        CandidateImportData importData)
    {
        IntakeRoundId = importData.IntakeRoundId;
        IntakeSource = CandidateIntakeSource.Email;
        ReviewStatus = CandidateReviewStatus.New;
        HireOutcome = CandidateHireOutcome.None;
        ExtractionStatus = CandidateExtractionStatus.Pending;
        SourceSenderName = importData.SourceSenderName;
        SourceSenderEmail = importData.SourceSenderEmail;
        SourceSubject = importData.SourceSubject;
        SourceBodyText = importData.SourceBodyText;
        SourceSentAt = importData.SourceSentAt;
        SourceOriginalFilename = importData.SourceOriginalFilename;
        SourceStorageKey = importData.SourceStorageKey;
        SourceSizeBytes = importData.SourceSizeBytes;
        SourceSha256 = importData.SourceSha256.ToArray();
        ImportedAt = importData.ImportedAt;
    }

    private Candidate(CandidateFormImportData importData)
    {
        IntakeRoundId = importData.IntakeRoundId;
        IntakeSource = CandidateIntakeSource.Form;
        ReviewStatus = CandidateReviewStatus.New;
        HireOutcome = CandidateHireOutcome.None;
        ExtractionStatus = CandidateExtractionStatus.Pending;
        ImportedAt = importData.ImportedAt;
    }

    public long IntakeRoundId { get; private set; }
    public CandidateIntakeSource IntakeSource { get; private set; }
    public bool IsResubmitted { get; private set; }
    public int? PromotedFromRoundNumber { get; private set; }
    public DateTimeOffset? PromotedAt { get; private set; }
    public CandidateReviewStatus ReviewStatus { get; private set; }
    public CandidateHireOutcome HireOutcome { get; private set; }
    public CandidateExtractionStatus ExtractionStatus { get; private set; }
    public string? FullName { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? Notes { get; private set; }
    public string? SourceSenderName { get; private set; }
    public string? SourceSenderEmail { get; private set; }
    public string? SourceSubject { get; private set; }
    public string? SourceBodyText { get; private set; }
    public DateTimeOffset? SourceSentAt { get; private set; }
    public string? SourceOriginalFilename { get; private set; }
    public string? SourceStorageKey { get; private set; }
    public long? SourceSizeBytes { get; private set; }
    public byte[]? SourceSha256 { get; private set; }
    public DateTimeOffset ImportedAt { get; private set; }
    public IReadOnlyList<CvDocument> CvDocuments => _cvDocuments;
    public IReadOnlyList<CandidateRequirementReview> RequirementReviews => _requirementReviews;
    public IReadOnlyList<CandidateFormResponse> FormResponses => _formResponses;

    internal Result UpdateDetails(string? fullName, string? contactEmail)
    {
        var detailsResult = CandidateDetailsRules.Validate(fullName, contactEmail);
        if (detailsResult.IsFailure)
        {
            return detailsResult;
        }

        FullName = detailsResult.Value.FullName;
        ContactEmail = detailsResult.Value.ContactEmail;
        return Result.Success();
    }

    internal Result UpdateNotes(string? notes)
    {
        var notesResult = CandidateNotesRules.Replace(notes);
        if (notesResult.IsFailure)
        {
            return notesResult;
        }

        Notes = notesResult.Value;
        return Result.Success();
    }

    internal Result ApplyReview(CandidateReviewStatus status, string? notes)
    {
        if (HireOutcome is CandidateHireOutcome.Hired or CandidateHireOutcome.Runaway)
        {
            return CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["reviewStatus"] = ["Review status cannot change while the hire outcome is hired or runaway."]
            });
        }

        var statusResult = CandidateReviewTransitions.Validate(status);
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        var notesResult = CandidateNotesRules.Replace(notes);
        if (notesResult.IsFailure)
        {
            return notesResult;
        }

        ReviewStatus = status;
        Notes = notesResult.Value;
        return Result.Success();
    }

    internal Result SetHireOutcome(CandidateHireOutcome outcome, string? note = null)
    {
        if (outcome != CandidateHireOutcome.None && ReviewStatus != CandidateReviewStatus.Shortlisted)
        {
            return CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["reviewStatus"] = ["A hire outcome can only be set for a shortlisted candidate."]
            });
        }

        var transitionResult = CandidateHireOutcomeTransitions.Calculate(
            HireOutcome,
            outcome,
            Notes,
            note);
        if (transitionResult.IsFailure)
        {
            return transitionResult;
        }

        HireOutcome = transitionResult.Value.Outcome;
        Notes = transitionResult.Value.Notes;
        return Result.Success();
    }

    internal bool CanBePromoted =>
        CandidatePromotionRules.CanBePromoted(ReviewStatus, HireOutcome);

    internal Result PromoteTo(long targetRoundId, int sourceRoundNumber, DateTimeOffset promotedAt)
    {
        if (!CanBePromoted)
        {
            return CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["candidateId"] = ["Candidate is not promotable from its current round."]
            });
        }

        var detailsResult = CandidatePromotionRules.ValidateDetails(
            targetRoundId,
            sourceRoundNumber,
            promotedAt);
        if (detailsResult.IsFailure)
        {
            return detailsResult;
        }

        IntakeRoundId = targetRoundId;
        PromotedFromRoundNumber = sourceRoundNumber;
        PromotedAt = promotedAt;
        return Result.Success();
    }

    internal Result SetRequirementReview(long vacancyRequirementId, bool confirmed)
    {
        var validationResult = CandidateRequirementReviewRules.Validate(vacancyRequirementId);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        var review = _requirementReviews
            .SingleOrDefault(item => item.VacancyRequirementId == vacancyRequirementId);
        if (review is null)
        {
            _requirementReviews.Add(new CandidateRequirementReview(
                Id,
                vacancyRequirementId,
                confirmed));
        }
        else
        {
            review.SetConfirmed(confirmed);
        }

        return Result.Success();
    }

    internal static Result<Candidate> Import(CandidateImportData importData)
    {
        var validationResult = CandidateImport.Validate(importData);
        if (validationResult.IsFailure)
        {
            return Result<Candidate>.Failure(validationResult.Error);
        }

        var candidate = new Candidate(importData);

        foreach (var document in CandidateImport.OrderDocuments(importData.CvDocuments))
        {
            candidate._cvDocuments.Add(new CvDocument(
                document.OriginalFilename,
                document.StorageKey,
                document.Position,
                document.IsPrimary,
                document.SizeBytes,
                document.Sha256));
        }

        return candidate;
    }

    internal static Result<Candidate> ImportForm(CandidateFormImportData importData)
    {
        if (importData.IntakeRoundId <= 0)
        {
            return Result<Candidate>.Failure(CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["intakeRoundId"] = ["Intake round is required."]
            }));
        }

        if (importData.ImportedAt == default)
        {
            return Result<Candidate>.Failure(CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["importedAt"] = ["The candidate import timestamp is required."]
            }));
        }

        return new Candidate(importData);
    }

    internal Result<CandidateFormResponse> AddFormResponse(
        CandidateFormResponseData responseData,
        CandidateFormResponse? currentResponse,
        bool isResubmitted)
    {
        var responseResult = CandidateFormResponse.Create(responseData);
        if (responseResult.IsFailure)
        {
            return responseResult;
        }

        currentResponse?.MarkPrior();
        _formResponses.Add(responseResult.Value);
        IsResubmitted |= isResubmitted;
        return responseResult.Value;
    }
}
