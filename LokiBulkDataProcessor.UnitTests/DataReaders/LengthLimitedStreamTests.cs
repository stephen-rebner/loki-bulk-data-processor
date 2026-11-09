using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Loki.BulkDataProcessor.DataReaders;
using LokiBulkDataProcessor.UnitTests.TestDoubles;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.DataReaders;

public class LengthLimitedStreamTests
{
    [Test]
    public async Task ReadAsync_ShouldReturnEveryByte_WhenTheContentIsWithinTheLimit()
    {
        var limited = new LengthLimitedStream(new NonSeekableStream(new byte[20]), maxBytes: 20);

        using var copy = new MemoryStream();
        await limited.CopyToAsync(copy);

        copy.Length.Should().Be(20);
    }

    [Test]
    public async Task ReadAsync_ShouldThrowInvalidDataException_WhenMoreThanTheLimitIsRead()
    {
        var inner = new NonSeekableStream(new byte[100]);
        var limited = new LengthLimitedStream(inner, maxBytes: 20);

        Func<Task> action = () => limited.CopyToAsync(Stream.Null);

        await action.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("The JSON payload is larger than the configured MaxJsonPayloadBytes limit of 20 bytes.");
        inner.BytesRead.Should().BeLessThan(100);
    }

    [Test]
    public void Dispose_ShouldNotDisposeTheCallersStream()
    {
        var inner = new MemoryStream(new byte[4]);

        new LengthLimitedStream(inner, maxBytes: 10).Dispose();

        inner.CanRead.Should().BeTrue();
    }

    [Test]
    public void ThrowIfKnownToExceed_ShouldThrowOnlyWhenTheRemainingSeekableLengthIsOverTheLimit()
    {
        var stream = new MemoryStream(new byte[30]) { Position = 10 };

        ((Action)(() => LengthLimitedStream.ThrowIfKnownToExceed(stream, 20))).Should().NotThrow();
        ((Action)(() => LengthLimitedStream.ThrowIfKnownToExceed(stream, 19))).Should().Throw<InvalidDataException>();
        ((Action)(() => LengthLimitedStream.ThrowIfKnownToExceed(new NonSeekableStream(new byte[30]), 1))).Should().NotThrow();
    }
}
