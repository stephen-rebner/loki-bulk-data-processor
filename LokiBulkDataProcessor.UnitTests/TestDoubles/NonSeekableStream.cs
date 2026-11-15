using System;
using System.IO;

namespace LokiBulkDataProcessor.UnitTests.TestDoubles;

/// <summary>A forward-only stream (like a request body) that counts the bytes handed out, in small chunks.</summary>
internal sealed class NonSeekableStream(byte[] content, int chunkSize = 8) : Stream
{
    private readonly MemoryStream _inner = new(content);

    public long BytesRead { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _inner.Read(buffer, offset, Math.Min(count, chunkSize));
        BytesRead += read;
        return read;
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
