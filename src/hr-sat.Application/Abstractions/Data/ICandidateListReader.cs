namespace hr_sat.Application.Abstractions.Data;

public interface ICandidateListReader
{
    Task<CandidateListReadResult> ReadAsync(
        CandidateListReadRequest request,
        CancellationToken cancellationToken);
}