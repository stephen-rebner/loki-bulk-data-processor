using System.Text.Json;
using FluentAssertions;
using Loki.BulkDataProcessor.Providers;
using Loki.Samples.WebApi.Errors;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;

namespace Loki.Samples.WebApi.Tests;

public class ProblemMappingTests
{
    private const string DriverText = "Violation of UNIQUE KEY constraint. The duplicate key value is (secret-row-value).";

    private static IEnumerable<TestCaseData> MappedExceptions()
    {
        yield return new TestCaseData(RequestRejectedException.NotFound("Unknown engine."), 404).SetName("RequestRejected 404");
        yield return new TestCaseData(RequestRejectedException.Unavailable("Not configured."), 503).SetName("RequestRejected 503");
        yield return new TestCaseData(new InvalidDataException(DriverText), 400).SetName("InvalidDataException");
        yield return new TestCaseData(new JsonException(DriverText), 400).SetName("JsonException");
        yield return new TestCaseData(new FormatException(DriverText), 400).SetName("FormatException");
        yield return new TestCaseData(new InvalidOperationException("Bulk copy failed.", new FormatException(DriverText)), 400)
            .SetName("Loki failure with an inner FormatException");
        yield return new TestCaseData(new InvalidOperationException(BulkCopyErrorMessages.ColumnMappingMismatch), 400)
            .SetName("Loki column mapping mismatch");
        yield return new TestCaseData(new InvalidOperationException(BulkCopyErrorMessages.SourceColumnNotFound("Name")), 400)
            .SetName("Loki source column not found");
        yield return new TestCaseData(new ArgumentException(DriverText, "dataToProcess"), 400).SetName("ArgumentException");
        yield return new TestCaseData(new BadHttpRequestException(DriverText, 413), 413).SetName("Body too large");
        yield return new TestCaseData(new SimulatedFailureException(), 500).SetName("Simulated failure");
        yield return new TestCaseData(new InvalidOperationException(DriverText, new InvalidOperationException(DriverText)), 500)
            .SetName("Any other Loki failure");
    }

    [TestCaseSource(nameof(MappedExceptions))]
    public void For_ShouldAnswerTheStatusWithAFixedDetail_WhenTheExceptionIsKnown(Exception exception, int expectedStatus)
    {
        Problem problem = ProblemMapping.For(exception);

        problem.StatusCode.Should().Be(expectedStatus);
        problem.Detail.Should().NotContain("secret-row-value");
    }

    [Test]
    public void For_ShouldAnswerClientClosedRequest_WhenTheRequestWasAborted()
    {
        ProblemMapping.For(new OperationCanceledException(), requestAborted: true).StatusCode.Should().Be(ProblemMapping.ClientClosedRequest);
    }

    [Test]
    public void For_ShouldAnswer500_WhenACancellationWasNotTheCallers()
    {
        ProblemMapping.For(new OperationCanceledException(), requestAborted: false).StatusCode.Should().Be(500);
    }
}
