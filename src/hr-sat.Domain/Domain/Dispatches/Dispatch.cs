using hr_sat.Domain;
using hr_sat.Domain.EmailTemplates;

namespace hr_sat.Domain.Dispatches;

public sealed class Dispatch : Entity
{
    private Dispatch()
    {
    }

    private Dispatch(
        long dispatchRunId,
        long candidateId,
        EmailTemplateKind templateKind,
        string renderedSubject,
        string renderedBody,
        DispatchStatus status,
        string? errorMessage,
        DateTimeOffset attemptedAt)
    {
        DispatchRunId = dispatchRunId;
        CandidateId = candidateId;
        TemplateKind = templateKind;
        RenderedSubject = renderedSubject;
        RenderedBody = renderedBody;
        Status = status;
        ErrorMessage = errorMessage;
        AttemptedAt = attemptedAt;
    }

    public long DispatchRunId { get; private set; }
    public long CandidateId { get; private set; }
    public EmailTemplateKind TemplateKind { get; private set; }
    public string RenderedSubject { get; private set; } = string.Empty;
    public string RenderedBody { get; private set; } = string.Empty;
    public DispatchStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset AttemptedAt { get; private set; }

    internal static Dispatch RecordSent(
        long dispatchRunId,
        long candidateId,
        EmailTemplateKind templateKind,
        string renderedSubject,
        string renderedBody,
        DateTimeOffset attemptedAt) =>
        new(
            dispatchRunId,
            candidateId,
            templateKind,
            renderedSubject,
            renderedBody,
            DispatchStatus.Sent,
            null,
            attemptedAt);

    internal static Dispatch RecordFailed(
        long dispatchRunId,
        long candidateId,
        EmailTemplateKind templateKind,
        string renderedSubject,
        string renderedBody,
        string errorMessage,
        DateTimeOffset attemptedAt) =>
        new(
            dispatchRunId,
            candidateId,
            templateKind,
            renderedSubject,
            renderedBody,
            DispatchStatus.Failed,
            errorMessage,
            attemptedAt);

    internal void MarkSent(DateTimeOffset attemptedAt)
    {
        Status = DispatchStatus.Sent;
        ErrorMessage = null;
        AttemptedAt = attemptedAt;
    }

    internal void MarkFailed(string errorMessage, DateTimeOffset attemptedAt)
    {
        Status = DispatchStatus.Failed;
        ErrorMessage = errorMessage;
        AttemptedAt = attemptedAt;
    }
}
