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
        string fromAddress,
        DispatchStatus status,
        string? errorMessage,
        DateTimeOffset attemptedAt)
    {
        DispatchRunId = dispatchRunId;
        CandidateId = candidateId;
        TemplateKind = templateKind;
        RenderedSubject = renderedSubject;
        RenderedBody = renderedBody;
        FromAddress = fromAddress;
        Status = status;
        ErrorMessage = errorMessage;
        AttemptedAt = attemptedAt;
    }

    public long DispatchRunId { get; private set; }
    public long CandidateId { get; private set; }
    public EmailTemplateKind TemplateKind { get; private set; }
    public string RenderedSubject { get; private set; } = string.Empty;
    public string RenderedBody { get; private set; } = string.Empty;
    // Which SMTP Account sent (or attempted) this Dispatch — part of the retained
    // snapshot so "which account sent this" stays answerable (issue 02, decision 7).
    public string FromAddress { get; private set; } = string.Empty;
    public DispatchStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset AttemptedAt { get; private set; }

    internal static Dispatch RecordSent(
        long dispatchRunId,
        long candidateId,
        EmailTemplateKind templateKind,
        string renderedSubject,
        string renderedBody,
        string fromAddress,
        DateTimeOffset attemptedAt) =>
        new(
            dispatchRunId,
            candidateId,
            templateKind,
            renderedSubject,
            renderedBody,
            fromAddress,
            DispatchStatus.Sent,
            null,
            attemptedAt);

    internal static Dispatch RecordFailed(
        long dispatchRunId,
        long candidateId,
        EmailTemplateKind templateKind,
        string renderedSubject,
        string renderedBody,
        string fromAddress,
        string errorMessage,
        DateTimeOffset attemptedAt) =>
        new(
            dispatchRunId,
            candidateId,
            templateKind,
            renderedSubject,
            renderedBody,
            fromAddress,
            DispatchStatus.Failed,
            errorMessage,
            attemptedAt);

    internal void MarkSent(string fromAddress, DateTimeOffset attemptedAt)
    {
        Status = DispatchStatus.Sent;
        ErrorMessage = null;
        FromAddress = fromAddress;
        AttemptedAt = attemptedAt;
    }

    internal void MarkFailed(string errorMessage, string fromAddress, DateTimeOffset attemptedAt)
    {
        Status = DispatchStatus.Failed;
        ErrorMessage = errorMessage;
        FromAddress = fromAddress;
        AttemptedAt = attemptedAt;
    }
}
