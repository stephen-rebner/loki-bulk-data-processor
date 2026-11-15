using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.DataReaders;

/// <summary>
/// A read-only view of a caller's stream that throws <see cref="InvalidDataException"/> as soon as more than
/// <see cref="MaxBytes"/> bytes have been read, so an oversized JSON payload is rejected before it is fully buffered.
/// Disposing it does not dispose the caller's stream.
/// </summary>
internal sealed class LengthLimitedStream(Stream inner, long maxBytes) : Stream
{
    private long _bytesRead;

    public long MaxBytes { get; } = maxBytes;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _bytesRead;
        set => throw new NotSupportedException();
    }

    /// <summary>The message used when the limit is exceeded; it names only the limit, never payload content.</summary>
    public static string TooLargeMessage(long maxBytes) =>
        $"The JSON payload is larger than the configured MaxJsonPayloadBytes limit of {maxBytes} bytes.";

    /// <summary>Throws early when a seekable stream already reports more remaining bytes than the limit.</summary>
    public static void ThrowIfKnownToExceed(Stream stream, long maxBytes)
    {
        if (stream.CanSeek && stream.Length - stream.Position > maxBytes)
        {
            throw new InvalidDataException(TooLargeMessage(maxBytes));
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

    public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Count(await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false));

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private int Count(int read)
    {
        _bytesRead += read;
        if (_bytesRead > MaxBytes)
        {
            throw new InvalidDataException(TooLargeMessage(MaxBytes));
        }

        return read;
    }
}
