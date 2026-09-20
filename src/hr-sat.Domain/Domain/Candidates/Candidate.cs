using hr_sat.Domain;
using hr_sat.Domain.Vacancies;
using hr_sat.Domain.Vacancies.FormLayouts;
using hr_sat.Domain.Candidates.FormResponses;

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
    public bool ScreenedOut { get; private set; }
    public IReadOnlyList<ScreeningRuleMatch>? ScreeningVerdict { get; private set; }
    public string? FullName { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public CandidateDetailProvenance FullNameProvenance { get; private set; }
    public CandidateDetailProvenance ContactEmailProvenance { get; private set; }
    public CandidateDetailProvenance ContactPhoneProvenance { get; private set; }
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

    internal Result UpdateDetails(
        string? fullName,
        string? contactEmail,
        string? contactPhone = null)
    {
        var detailsResult = CandidateDetailsRules.Validate(
            fullName,
            contactEmail,
            contactPhone);
        if (detailsResult.IsFailure)
        {
            return detailsResult;
        }

        FullName = detailsResult.Value.FullName;
        ContactEmail = detailsResult.Value.ContactEmail;
        ContactPhone = detailsResult.Value.ContactPhone;
        FullNameProvenance = CandidateDetailProvenance.Typed;
        ContactEmailProvenance = CandidateDetailProvenance.Typed;
        ContactPhoneProvenance = CandidateDetailProvenance.Typed;
        return Result.Success();
    }

    internal CandidatePrefillResult PrefillDetailsFromLayout(FormLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (IntakeSource != CandidateIntakeSource.Form)
        {
            return new CandidatePrefillResult(false, 0);
        }

        var currentResponse = _formResponses.SingleOrDefault(response => response.IsCurrent);
        if (currentResponse is null)
        {
            return new CandidatePrefillResult(false, 0);
        }

        var updated = false;
        var typedOverridesKept = 0;
        if (FullNameProvenance == CandidateDetailProvenance.Typed)
        {
            typedOverridesKept++;
        }
        else
        {
            var value = ReadCell(currentResponse.Cells, layout.GetColumnOrdinal(FormLayoutRole.Name));
            var normalized = CandidateDetailsRules.TryNormalizeFullName(value, out var fullName);
            var nextValue = normalized ? fullName : null;
            var nextProvenance = nextValue is null
                ? CandidateDetailProvenance.None
                : CandidateDetailProvenance.FormPrefilled;
            if (FullName != nextValue || FullNameProvenance != nextProvenance)
            {
                FullName = nextValue;
                FullNameProvenance = nextProvenance;
                updated = true;
            }
        }

        if (ContactEmailProvenance == CandidateDetailProvenance.Typed)
        {
            typedOverridesKept++;
        }
        else
        {
            var value = ReadCell(
                currentResponse.Cells,
                layout.GetColumnOrdinal(FormLayoutRole.ContactEmail));
            var normalized = CandidateDetailsRules.TryNormalizeContactEmail(value, out var contactEmail);
            var nextValue = normalized ? contactEmail : null;
            var nextProvenance = nextValue is null
                ? CandidateDetailProvenance.None
                : CandidateDetailProvenance.FormPrefilled;
            if (ContactEmail != nextValue || ContactEmailProvenance != nextProvenance)
            {
                ContactEmail = nextValue;
                ContactEmailProvenance = nextProvenance;
                updated = true;
            }
        }

        if (ContactPhoneProvenance == CandidateDetailProvenance.Typed)
        {
            typedOverridesKept++;
        }
        else
        {
            var value = ReadCell(
                currentResponse.Cells,
                layout.GetColumnOrdinal(FormLayoutRole.ContactPhone));
            var normalized = CandidateDetailsRules.TryNormalizeContactPhone(value, out var contactPhone);
            var nextValue = normalized ? contactPhone : null;
            var nextProvenance = nextValue is null
                ? CandidateDetailProvenance.None
                : CandidateDetailProvenance.FormPrefilled;
            if (ContactPhone != nextValue || ContactPhoneProvenance != nextProvenance)
            {
                ContactPhone = nextValue;
                ContactPhoneProvenance = nextProvenance;
                updated = true;
            }
        }

        return new CandidatePrefillResult(updated, typedOverridesKept);
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
        ScreenedOut = false;
        ScreeningVerdict = null;
        return Result.Success();
    }

    internal void FreezeScreening(ScreeningContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Kind is ScreeningContextKind.Frozen)
        {
            throw new ArgumentException(
                "Screening cannot be frozen from a frozen context.",
                nameof(context));
        }

        var firedRules = EvaluateScreening(context);
        ScreenedOut = firedRules.Count > 0;
        ScreeningVerdict = firedRules;
    }

    internal IReadOnlyList<ScreeningRuleMatch> EvaluateScreening(ScreeningContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IntakeSource != CandidateIntakeSource.Form)
        {
            return [];
        }

        if (context.Kind is ScreeningContextKind.Frozen)
        {
            return ScreenedOut ? ScreeningVerdict ?? [] : [];
        }

        if (context.Kind is not ScreeningContextKind.Live)
        {
            return [];
        }

        var currentResponse = _formResponses.SingleOrDefault(response => response.IsCurrent);
        return currentResponse is null
            ? []
            : context.RuleSet!.EvaluateWithDisplay(currentResponse.Cells, context.Layout!);
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

    /// <summary>
    /// The candidate's received moment: the source email's sent date, or the current
    /// Form Response's parsed timestamp for form-sourced candidates. Null when neither
    /// exists; the candidate list displays and sorts on this.
    /// </summary>
    public DateTimeOffset? ReceivedAt =>
        SourceSentAt ?? _formResponses.SingleOrDefault(response => response.IsCurrent)?.FormTimestampParsed;

    /// <summary>
    /// The form-sourced candidate's CV link, read from the current Form Response through
    /// the layout's CV Link ordinal. Null for email candidates, when no layout/role is
    /// bound, or when the cell is empty — never throws on a short row.
    /// </summary>
    public string? CvLink(FormLayout? layout)
    {
        if (IntakeSource != CandidateIntakeSource.Form || layout is null)
        {
            return null;
        }

        var currentResponse = _formResponses.SingleOrDefault(response => response.IsCurrent);
        var value = currentResponse is null
            ? null
            : ReadCell(currentResponse.Cells, layout.GetColumnOrdinal(FormLayoutRole.CvLink));
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? ReadCell(
        IReadOnlyList<string> cells,
        int? ordinal) =>
        ordinal.HasValue && ordinal.Value < cells.Count
            ? cells[ordinal.Value]
            : null;
}
