using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

public sealed class Candidate : Entity
{
    private readonly List<CvDocument> _cvDocuments = [];
    private readonly List<CandidateSkill> _skills = [];

    private Candidate()
    {
    }

    private Candidate(
        long vacancyId,
        string? sourceSenderName,
        string? sourceSenderEmail,
        string? sourceSubject,
        string? sourceBodyText,
        DateTimeOffset? sourceSentAt,
        string sourceOriginalFilename,
        string sourceStorageKey,
        long sourceSizeBytes,
        byte[] sourceSha256,
        DateTimeOffset importedAt)
    {
        VacancyId = vacancyId;
        ReviewStatus = CandidateReviewStatus.New;
        ExtractionStatus = CandidateExtractionStatus.Pending;
        SourceSenderName = sourceSenderName;
        SourceSenderEmail = sourceSenderEmail;
        SourceSubject = sourceSubject;
        SourceBodyText = sourceBodyText;
        SourceSentAt = sourceSentAt;
        SourceOriginalFilename = sourceOriginalFilename;
        SourceStorageKey = sourceStorageKey;
        SourceSizeBytes = sourceSizeBytes;
        SourceSha256 = sourceSha256.ToArray();
        ImportedAt = importedAt;
    }

    public long VacancyId { get; private set; }
    public CandidateReviewStatus ReviewStatus { get; private set; }
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
    public string SourceOriginalFilename { get; private set; } = string.Empty;
    public string SourceStorageKey { get; private set; } = string.Empty;
    public long SourceSizeBytes { get; private set; }
    public byte[] SourceSha256 { get; private set; } = [];
    public DateTimeOffset ImportedAt { get; private set; }
    public IReadOnlyList<CvDocument> CvDocuments => _cvDocuments;
    public IReadOnlyList<CandidateSkill> Skills => _skills;

    internal Result ApplyExtraction(CandidateExtraction extraction)
    {
        ArgumentNullException.ThrowIfNull(extraction);

        var errors = new Dictionary<string, string[]>();
        ValidateOptionalText(errors, "fullName", extraction.FullName, 300);
        ValidateOptionalText(errors, "contactEmail", extraction.ContactEmail, 320);
        ValidateOptionalText(errors, "contactPhone", extraction.ContactPhone, 100);

        var skills = extraction.Skills ?? [];
        if (skills.Any(skill => skill is null || skill.Trim().Length is < 1 or > 200))
        {
            errors["skills"] = [
                "Each candidate skill must contain between 1 and 200 characters after trimming."
            ];
        }
        else if (skills
            .GroupBy(skill => skill.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            errors["skills"] = [
                "Candidate skills must be unique after trimming and ignoring case."
            ];
        }

        if (errors.Count > 0)
        {
            return CandidateErrors.Invalid(errors);
        }

        FullName = TrimOptional(extraction.FullName);
        ContactEmail = TrimOptional(extraction.ContactEmail);
        ContactPhone = TrimOptional(extraction.ContactPhone);
        _skills.Clear();

        var position = 1;
        foreach (var skill in skills)
        {
            _skills.Add(new CandidateSkill(skill.Trim(), position));
            position++;
        }

        ExtractionStatus = CandidateExtractionStatus.Succeeded;
        return Result.Success();
    }

    internal void MarkExtractionFailed()
    {
        FullName = null;
        ContactEmail = null;
        ContactPhone = null;
        _skills.Clear();
        ExtractionStatus = CandidateExtractionStatus.Failed;
    }

    internal void MarkExtractionPending()
    {
        FullName = null;
        ContactEmail = null;
        ContactPhone = null;
        _skills.Clear();
        ExtractionStatus = CandidateExtractionStatus.Pending;
    }

    internal Result SelectPrimaryCv(long documentId)
    {
        var document = _cvDocuments.SingleOrDefault(item => item.Id == documentId);
        if (document is null)
        {
            return CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["documentId"] = ["The CV document does not belong to this candidate."]
            });
        }

        foreach (var cvDocument in _cvDocuments)
        {
            cvDocument.SetPrimary(cvDocument.Id == documentId);
        }

        MarkExtractionPending();
        return Result.Success();
    }

    internal static Result<Candidate> Import(
        long vacancyId,
        string? sourceSenderName,
        string? sourceSenderEmail,
        string? sourceSubject,
        string? sourceBodyText,
        DateTimeOffset? sourceSentAt,
        string sourceOriginalFilename,
        string sourceStorageKey,
        long sourceSizeBytes,
        byte[] sourceSha256,
        DateTimeOffset importedAt,
        IReadOnlyList<StoredCvDocument> cvDocuments)
    {
        var sourceResult = ValidateSource(
            vacancyId,
            sourceOriginalFilename,
            sourceStorageKey,
            sourceSizeBytes,
            sourceSha256,
            importedAt,
            sourceSenderName,
            sourceSenderEmail);
        if (sourceResult.IsFailure)
        {
            return Result<Candidate>.Failure(sourceResult.Error);
        }

        if (cvDocuments.Count == 0)
        {
            return Result<Candidate>.Failure(CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["documents"] = ["At least one CV document is required."]
            }));
        }

        var hasDuplicatePosition = cvDocuments
            .GroupBy(document => document.Position)
            .Any(group => group.Count() > 1);
        if (hasDuplicatePosition)
        {
            return Result<Candidate>.Failure(CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["documents"] = ["CV document positions must be unique."]
            }));
        }

        var candidate = new Candidate(
            vacancyId,
            sourceSenderName,
            sourceSenderEmail,
            sourceSubject,
            sourceBodyText,
            sourceSentAt,
            sourceOriginalFilename,
            sourceStorageKey,
            sourceSizeBytes,
            sourceSha256,
            importedAt);

        foreach (var document in cvDocuments.OrderBy(document => document.Position))
        {
            var documentResult = ValidateDocument(document);
            if (documentResult.IsFailure)
            {
                return Result<Candidate>.Failure(documentResult.Error);
            }

            candidate._cvDocuments.Add(new CvDocument(
                document.OriginalFilename,
                document.StorageKey,
                document.Position,
                document.IsPrimary,
                document.SizeBytes,
                document.Sha256));
        }

        if (cvDocuments.Count == 1 && !cvDocuments[0].IsPrimary)
        {
            return Result<Candidate>.Failure(CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["documents"] = ["A candidate with one CV document must have a primary document."]
            }));
        }

        if (cvDocuments.Count(document => document.IsPrimary) > 1)
        {
            return Result<Candidate>.Failure(CandidateErrors.Invalid(new Dictionary<string, string[]>
            {
                ["documents"] = ["A candidate can have at most one primary CV document."]
            }));
        }

        return candidate;
    }

    private static Result ValidateSource(
        long vacancyId,
        string sourceOriginalFilename,
        string sourceStorageKey,
        long sourceSizeBytes,
        byte[] sourceSha256,
        DateTimeOffset importedAt,
        string? sourceSenderName,
        string? sourceSenderEmail)
    {
        var errors = new Dictionary<string, string[]>();
        if (vacancyId <= 0)
        {
            errors["vacancyId"] = ["Vacancy is required."];
        }

        if (string.IsNullOrWhiteSpace(sourceOriginalFilename))
        {
            errors["sourceOriginalFilename"] = ["The source filename is required."];
        }

        if (string.IsNullOrWhiteSpace(sourceStorageKey))
        {
            errors["sourceStorageKey"] = ["The source storage key is required."];
        }

        if (sourceSizeBytes <= 0)
        {
            errors["sourceSizeBytes"] = ["The source email must not be empty."];
        }

        if (sourceSha256.Length != 32)
        {
            errors["sourceSha256"] = ["The source hash must be a SHA-256 hash."];
        }

        if (importedAt == default)
        {
            errors["importedAt"] = ["The import timestamp is required."];
        }

        ValidateOptionalText(errors, "sourceSenderName", sourceSenderName, 300);
        ValidateOptionalText(errors, "sourceSenderEmail", sourceSenderEmail, 320);

        if (errors.Count > 0)
        {
            return CandidateErrors.Invalid(errors);
        }

        return Result.Success();
    }

    private static void ValidateOptionalText(
        IDictionary<string, string[]> errors,
        string field,
        string? value,
        int maximumLength)
    {
        if (value is null)
        {
            return;
        }

        var length = value.Trim().Length;
        if (length < 1 || length > maximumLength)
        {
            errors[field] = [$"The value must contain between 1 and {maximumLength} characters after trimming."];
        }
    }

    private static string? TrimOptional(string? value) =>
        value is null ? null : value.Trim();

    private static Result ValidateDocument(StoredCvDocument document)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(document.OriginalFilename))
        {
            errors["documents"] = ["Each CV document filename is required."];
        }

        if (string.IsNullOrWhiteSpace(document.StorageKey))
        {
            errors["documents"] = ["Each CV document storage key is required."];
        }

        if (document.Position <= 0)
        {
            errors["documents"] = ["Each CV document position must be positive."];
        }

        if (document.SizeBytes <= 0)
        {
            errors["documents"] = ["Each CV document must not be empty."];
        }

        if (document.Sha256.Length != 32)
        {
            errors["documents"] = ["Each CV document hash must be a SHA-256 hash."];
        }

        if (errors.Count > 0)
        {
            return CandidateErrors.Invalid(errors);
        }

        return Result.Success();
    }
}

internal sealed record StoredCvDocument(
    string OriginalFilename,
    string StorageKey,
    int Position,
    bool IsPrimary,
    long SizeBytes,
    byte[] Sha256);