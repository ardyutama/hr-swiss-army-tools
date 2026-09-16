using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using hr_sat.Domain.EmailTemplates;
using hr_sat.Domain.IntakeRounds;

namespace hr_sat.Domain.Vacancies;

public sealed class Vacancy : Entity
{
    private readonly List<VacancyRequirement> _requirements = [];
    private readonly List<IntakeRound> _rounds = [];
    private readonly List<EmailTemplate> _emailTemplates = [];
    private FormLayout? _formLayout;

    private Vacancy()
    {
    }

    private Vacancy(string title, DateOnly openedOn, int? neededHires)
    {
        Title = title;
        OpenedOn = openedOn;
        NeededHires = neededHires;
        Status = VacancyStatus.Open;
    }

    public string Title { get; private set; } = string.Empty;
    public DateOnly OpenedOn { get; private set; }
    public int? NeededHires { get; private set; }
    public VacancyStatus Status { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyList<VacancyRequirement> Requirements => _requirements;
    public IReadOnlyList<IntakeRound> Rounds => _rounds;
    public IReadOnlyList<EmailTemplate> EmailTemplates => _emailTemplates;
    public FormLayout? FormLayout => _formLayout;
    public IntakeRound? ActiveRound => _rounds.SingleOrDefault(round => round.IsOpen);

    public Result<IntakeRound> CreateRound(string? name)
    {
        var createResult = VacancyRoundRules.EnsureCanCreateRound(Status, Id, ActiveRound);
        if (createResult.IsFailure)
        {
            return Result<IntakeRound>.Failure(createResult.Error);
        }

        var roundResult = IntakeRound.Create(VacancyRoundRules.NextRoundNumber(_rounds), name);
        if (roundResult.IsFailure)
        {
            return roundResult;
        }

        _rounds.Add(roundResult.Value);
        return roundResult.Value;
    }

    public Result CloseRound(long roundId, DateTimeOffset closedAt)
    {
        var round = _rounds.SingleOrDefault(item => item.Id == roundId);
        var closeResult = VacancyRoundRules.EnsureCanCloseRound(Status, round, roundId);
        return closeResult.IsFailure ? closeResult : round!.Close(closedAt);
    }

    public static Result<Vacancy> Create(
        string? title,
        DateOnly openedOn,
        IEnumerable<string?>? requirements,
        int? neededHires)
    {
        var requirementList = VacancyDefinitionRules.Validate(title, openedOn, requirements, neededHires);
        if (requirementList.IsFailure)
        {
            return Result<Vacancy>.Failure(requirementList.Error);
        }

        var vacancy = new Vacancy(title!, openedOn, neededHires);
        vacancy.ReplaceRequirements(requirementList.Value);
        vacancy._rounds.Add(IntakeRound.CreateDefault());
        return vacancy;
    }

    public Result UpdateDefinition(
        string? title,
        DateOnly openedOn,
        IEnumerable<string?>? requirements,
        int? neededHires)
    {
        var openResult = VacancyLifecycleRules.EnsureOpen(
            Status,
            "A closed vacancy must be reopened before it can be updated.");
        if (openResult.IsFailure)
        {
            return openResult;
        }

        var requirementList = VacancyDefinitionRules.Validate(title, openedOn, requirements, neededHires);
        if (requirementList.IsFailure)
        {
            return requirementList.Error;
        }

        Title = title!;
        OpenedOn = openedOn;
        NeededHires = neededHires;
        ReplaceRequirements(requirementList.Value);
        return Result.Success();
    }

    public Result<FormLayout> UpsertFormLayout(FormLayoutDefinition definition)
    {
        var openResult = VacancyLifecycleRules.EnsureOpen(
            Status,
            "A closed vacancy must be reopened before its Form Layout can be changed.");
        if (openResult.IsFailure)
        {
            return Result<FormLayout>.Failure(openResult.Error);
        }

        if (_formLayout is null)
        {
            var createResult = FormLayout.Create(Id, definition);
            if (createResult.IsFailure)
            {
                return createResult;
            }

            _formLayout = createResult.Value;
            return createResult.Value;
        }

        var replaceResult = _formLayout.Replace(definition);
        return replaceResult.IsFailure
            ? Result<FormLayout>.Failure(replaceResult.Error)
            : _formLayout;
    }

    public Result Close(DateTimeOffset closedAt)
    {
        var closeResult = VacancyLifecycleRules.Close(Status);
        if (closeResult.IsFailure)
        {
            return closeResult;
        }

        Status = VacancyStatus.Closed;
        ClosedAt = closedAt;
        return Result.Success();
    }

    public Result Reopen()
    {
        var reopenResult = VacancyLifecycleRules.Reopen(Status);
        if (reopenResult.IsFailure)
        {
            return reopenResult;
        }

        Status = VacancyStatus.Open;
        ClosedAt = null;
        return Result.Success();
    }

    public Result<EmailTemplate> UpsertEmailTemplate(
        EmailTemplateKind kind,
        string? subject,
        string? body)
    {
        var openResult = VacancyLifecycleRules.EnsureCanMutateEmailTemplate(Status, Id);
        if (openResult.IsFailure)
        {
            return Result<EmailTemplate>.Failure(openResult.Error);
        }

        var existing = _emailTemplates.SingleOrDefault(template => template.Kind == kind);
        if (existing is null)
        {
            var createResult = EmailTemplate.Create(Id, kind, subject, body);
            if (createResult.IsFailure)
            {
                return createResult;
            }

            _emailTemplates.Add(createResult.Value);
            Raise(new EmailTemplateUpsertedDomainEvent(Id, kind));
            return createResult.Value;
        }

        var replaceResult = existing.Replace(subject, body);
        if (replaceResult.IsFailure)
        {
            return Result<EmailTemplate>.Failure(replaceResult.Error);
        }

        Raise(new EmailTemplateUpsertedDomainEvent(Id, kind));
        return existing;
    }

    public Result DeleteEmailTemplate(EmailTemplateKind kind)
    {
        var kindResult = EmailTemplateRules.EnsureSupportedKind(kind);
        if (kindResult.IsFailure)
        {
            return kindResult;
        }

        var openResult = VacancyLifecycleRules.EnsureCanMutateEmailTemplate(Status, Id);
        if (openResult.IsFailure)
        {
            return openResult;
        }

        var existing = _emailTemplates.SingleOrDefault(template => template.Kind == kind);
        if (existing is null)
        {
            return EmailTemplateErrors.NotFound(Id, kind);
        }

        _emailTemplates.Remove(existing);
        Raise(new EmailTemplateDeletedDomainEvent(Id, kind));
        return Result.Success();
    }

    public Result<IntakeRound> EnsureCanReceiveCandidateImport(long roundId)
    {
        var openResult = VacancyLifecycleRules.EnsureCanReceiveCandidateImport(Status);
        return openResult.IsFailure
            ? Result<IntakeRound>.Failure(openResult.Error)
            : EnsureOpenRound(roundId, requireActive: true);
    }

    public Result<Candidate> ImportCandidate(CandidateImportData importData)
    {
        ArgumentNullException.ThrowIfNull(importData);

        var roundResult = EnsureOpenRound(importData.IntakeRoundId, requireActive: false);
        if (roundResult.IsFailure)
        {
            return Result<Candidate>.Failure(roundResult.Error);
        }

        var candidateResult = Candidate.Import(importData);
        if (candidateResult.IsFailure)
        {
            return candidateResult;
        }

        roundResult.Value.AddCandidate(candidateResult.Value);
        return candidateResult.Value;
    }

    public Result<Candidate> ImportFormCandidate(CandidateFormImportData importData)
    {
        ArgumentNullException.ThrowIfNull(importData);

        var roundResult = EnsureOpenRound(importData.IntakeRoundId, requireActive: false);
        if (roundResult.IsFailure)
        {
            return Result<Candidate>.Failure(roundResult.Error);
        }

        var candidateResult = Candidate.ImportForm(importData);
        if (candidateResult.IsFailure)
        {
            return candidateResult;
        }

        roundResult.Value.AddCandidate(candidateResult.Value);
        return candidateResult.Value;
    }

    public Result<IntakeRound> EnsureCanRemoveCandidate(long roundId)
    {
        var openResult = VacancyLifecycleRules.EnsureCanRemoveCandidate(Status);
        return openResult.IsFailure
            ? Result<IntakeRound>.Failure(openResult.Error)
            : EnsureOpenRound(roundId, requireActive: false);
    }

    public Result<IntakeRound> EnsureCanReviewCandidate(long roundId)
    {
        var openResult = VacancyLifecycleRules.EnsureCanReviewCandidate(Status);
        return openResult.IsFailure
            ? Result<IntakeRound>.Failure(openResult.Error)
            : EnsureOpenRound(roundId, requireActive: false);
    }

    public Result<IntakeRound> EnsureCanRecordHireOutcome(long roundId)
    {
        var openResult = VacancyLifecycleRules.EnsureCanRecordHireOutcome(Status, Id);
        if (openResult.IsFailure)
        {
            return Result<IntakeRound>.Failure(openResult.Error);
        }

        var round = _rounds.SingleOrDefault(item => item.Id == roundId);
        return round is null
            ? Result<IntakeRound>.Failure(IntakeRoundErrors.NotFound(roundId))
            : round;
    }

    public Result<Candidate> UpdateCandidateDetails(
        long roundId,
        long candidateId,
        string? fullName,
        string? contactEmail) =>
        MutateCandidate(
            roundId,
            candidateId,
            EnsureCanReviewCandidate,
            candidate => candidate.UpdateDetails(fullName, contactEmail));

    public Result<Candidate> UpdateCandidateNotes(
        long roundId,
        long candidateId,
        string? notes) =>
        MutateCandidate(
            roundId,
            candidateId,
            EnsureCanReviewCandidate,
            candidate => candidate.UpdateNotes(notes));

    public Result<Candidate> ReviewCandidate(
        long roundId,
        long candidateId,
        CandidateReviewStatus status,
        string? notes) =>
        MutateCandidate(
            roundId,
            candidateId,
            EnsureCanReviewCandidate,
            candidate => candidate.ApplyReview(status, notes));

    public Result<Candidate> ReviewCandidateRequirement(
        long roundId,
        long candidateId,
        long vacancyRequirementId,
        bool confirmed) =>
        MutateCandidate(
            roundId,
            candidateId,
            EnsureCanReviewCandidate,
            candidate => candidate.SetRequirementReview(vacancyRequirementId, confirmed));

    public Result<Candidate> SetCandidateHireOutcome(
        long roundId,
        long candidateId,
        CandidateHireOutcome outcome,
        string? note) =>
        MutateCandidate(
            roundId,
            candidateId,
            EnsureCanRecordHireOutcome,
            candidate => candidate.SetHireOutcome(outcome, note));

    public Result<IReadOnlyList<Candidate>> PromoteCandidates(
        long sourceRoundId,
        IEnumerable<long>? candidateIds,
        DateTimeOffset promotedAt)
    {
        var sourceRound = _rounds.SingleOrDefault(round => round.Id == sourceRoundId);
        var activeRound = ActiveRound;
        var validationResult = VacancyPromotionRules.ValidatePromotion(
            Status,
            Id,
            sourceRoundId,
            sourceRound,
            activeRound,
            candidateIds,
            _rounds.SelectMany(round => round.Candidates).ToList());
        if (validationResult.IsFailure)
        {
            return Result<IReadOnlyList<Candidate>>.Failure(validationResult.Error);
        }

        var candidates = validationResult.Value;
        foreach (var candidate in candidates)
        {
            var promoteResult = candidate.PromoteTo(
                activeRound!.Id,
                sourceRound!.RoundNumber,
                promotedAt);
            if (promoteResult.IsFailure)
            {
                return Result<IReadOnlyList<Candidate>>.Failure(promoteResult.Error);
            }
        }

        return Result<IReadOnlyList<Candidate>>.Success(candidates);
    }

    private Result<Candidate> MutateCandidate(
        long roundId,
        long candidateId,
        Func<long, Result<IntakeRound>> ensureCanMutate,
        Func<Candidate, Result> mutation)
    {
        var roundResult = ensureCanMutate(roundId);
        if (roundResult.IsFailure)
        {
            return Result<Candidate>.Failure(roundResult.Error);
        }

        var candidate = roundResult.Value.Candidates
            .SingleOrDefault(item => item.Id == candidateId);
        if (candidate is null)
        {
            return Result<Candidate>.Failure(CandidateErrors.NotFound(candidateId));
        }

        var mutationResult = mutation(candidate);
        return mutationResult.IsFailure
            ? Result<Candidate>.Failure(mutationResult.Error)
            : Result<Candidate>.Success(candidate);
    }

    private Result<IntakeRound> EnsureOpenRound(long roundId, bool requireActive) =>
        VacancyRoundRules.EnsureOpenRound(
            _rounds.SingleOrDefault(item => item.Id == roundId),
            roundId,
            requireActive,
            ActiveRound?.Id,
            Id);

    private void ReplaceRequirements(IReadOnlyList<string> requirements)
    {
        _requirements.Clear();
        var position = 1;

        foreach (var requirement in requirements)
        {
            _requirements.Add(new VacancyRequirement(requirement, position));
            position++;
        }
    }
}