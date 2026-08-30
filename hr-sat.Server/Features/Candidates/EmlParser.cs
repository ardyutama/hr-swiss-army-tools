using MimeKit;

namespace hr_sat.Server.Features.Candidates;

internal static class EmlParser
{
    private const int MaxPdfAttachmentCount = 16;
    private const long MaxPdfAttachmentSizeBytes = 25 * 1024 * 1024;
    private const long MaxPdfBatchSizeBytes = 50 * 1024 * 1024;

    public static ParsedEml Parse(byte[] sourceBytes, CancellationToken cancellationToken)
    {
        using var sourceStream = new MemoryStream(sourceBytes, writable: false);
        var message = MimeMessage.Load(sourceStream, cancellationToken);
        var sender = message.From.Mailboxes.FirstOrDefault();
        var attachments = new List<ParsedPdfAttachment>();
        long decodedPdfBytes = 0;

        foreach (var attachment in message.Attachments.OfType<MimePart>())
        {
            var isPdfContentType = string.Equals(
                attachment.ContentType?.MimeType,
                "application/pdf",
                StringComparison.OrdinalIgnoreCase);
            var isPdfFilename = string.Equals(
                Path.GetExtension(attachment.FileName),
                ".pdf",
                StringComparison.OrdinalIgnoreCase);

            if (!isPdfContentType && !isPdfFilename)
            {
                continue;
            }

            if (attachment.Content is null)
            {
                continue;
            }

            if (attachments.Count == MaxPdfAttachmentCount)
            {
                throw new InvalidDataException("The email contains too many PDF attachments.");
            }

            using var attachmentStream = new BoundedMemoryStream(MaxPdfAttachmentSizeBytes);
            attachment.Content.DecodeTo(attachmentStream);
            var content = attachmentStream.ToArray();
            if (!HasPdfSignature(content))
            {
                continue;
            }

            decodedPdfBytes += content.LongLength;
            if (decodedPdfBytes > MaxPdfBatchSizeBytes)
            {
                throw new InvalidDataException("The email contains too much PDF data.");
            }

            var filename = Path.GetFileName(attachment.FileName);
            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = $"attachment-{attachments.Count + 1}.pdf";
            }

            attachments.Add(new ParsedPdfAttachment(filename, content));
        }

        return new ParsedEml(
            string.IsNullOrWhiteSpace(sender?.Name) ? null : sender.Name,
            string.IsNullOrWhiteSpace(sender?.Address) ? null : sender.Address,
            message.Subject,
            message.TextBody ?? message.HtmlBody,
            message.Date == DateTimeOffset.MinValue ? null : message.Date,
            attachments);
    }

    private static bool HasPdfSignature(byte[] content) =>
        content.Length >= 5 &&
        content[0] == 0x25 &&
        content[1] == 0x50 &&
        content[2] == 0x44 &&
        content[3] == 0x46 &&
        content[4] == 0x2D;

    private sealed class BoundedMemoryStream(long maximumLength) : Stream
    {
        private readonly MemoryStream _inner = new();

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _inner.Read(buffer);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCapacity(count);
            _inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacity(buffer.Length);
            _inner.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            EnsureCapacity(1);
            _inner.WriteByte(value);
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        private void EnsureCapacity(int bytesToWrite)
        {
            if (bytesToWrite < 0 || Length > maximumLength - bytesToWrite)
            {
                throw new InvalidDataException("A PDF attachment exceeds the size limit.");
            }
        }

        public byte[] ToArray() => _inner.ToArray();
    }
}

internal sealed record ParsedEml(
    string? SenderName,
    string? SenderEmail,
    string? Subject,
    string? BodyText,
    DateTimeOffset? SentAt,
    IReadOnlyList<ParsedPdfAttachment> PdfAttachments);

internal sealed record ParsedPdfAttachment(string OriginalFilename, byte[] Content);