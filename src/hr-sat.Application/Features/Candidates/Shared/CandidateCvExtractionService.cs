using hr_sat.Application.Abstractions.Extraction;
using hr_sat.Application.Abstractions.Storage;
using hr_sat.Domain;
using hr_sat.Domain.Candidates;
using Microsoft.Extensions.Logging;

namespace hr_sat.Application.Features.Candidates.Shared;

internal sealed class CandidateCvExtractionService(
    IPdfTextExtractor pdfTextExtractor,
    ILogger<CandidateCvExtractionService> logger)
{
    public async Task<Result<CandidateExtraction>> ExtractAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        var text = await pdfTextExtractor.ExtractTextAsync(content, cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<CandidateExtraction>.Failure(CandidateErrors.ExtractionFailed());
        }

        return CandidateTextParser.Parse(text);
    }

    public async Task<bool> TryApplyAsync(
        Candidate candidate,
        IPrivateFileStorage fileStorage,
        string storageKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var content = await fileStorage.OpenReadAsync(storageKey, cancellationToken);
            var extractionResult = await ExtractAsync(content, cancellationToken);
            if (extractionResult.IsFailure)
            {
                candidate.MarkExtractionFailed();
                return false;
            }

            var applyResult = candidate.ApplyExtraction(extractionResult.Value);
            if (applyResult.IsFailure)
            {
                candidate.MarkExtractionFailed();
                logger.LogWarning(
                    "Extracted data was invalid for candidate {CandidateId}; marking extraction as failed.",
                    candidate.Id);
                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            candidate.MarkExtractionFailed();
            logger.LogWarning(
                exception,
                "CV extraction failed for candidate {CandidateId}; retaining the candidate with failed extraction status.",
                candidate.Id);
            return false;
        }
    }
}
