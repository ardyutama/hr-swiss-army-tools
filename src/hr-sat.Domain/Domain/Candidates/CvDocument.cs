using hr_sat.Domain;

namespace hr_sat.Domain.Candidates;

public sealed class CvDocument : Entity
{
    private CvDocument()
    {
    }

    internal CvDocument(
        string originalFilename,
        string storageKey,
        int position,
        bool isPrimary,
        long sizeBytes,
        byte[] sha256)
    {
        OriginalFilename = originalFilename;
        StorageKey = storageKey;
        Position = position;
        IsPrimary = isPrimary;
        SizeBytes = sizeBytes;
        Sha256 = sha256.ToArray();
    }

    public long CandidateId { get; private set; }
    public int Position { get; private set; }
    public bool IsPrimary { get; private set; }
    public string OriginalFilename { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public byte[] Sha256 { get; private set; } = [];

    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}