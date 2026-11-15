using FluentAssertions;
using Loki.BulkDataProcessor.Providers;
using NUnit.Framework;

namespace LokiBulkDataProcessor.UnitTests.Providers;

public class BulkCopyErrorMessagesTests
{
    [Test]
    public void BulkCopyFailed_ShouldBuildTheFixedMessage_WhenAnErrorCodeIsOrIsNotGiven()
    {
        BulkCopyErrorMessages.BulkCopyFailed("dbo.Posts")
            .Should().Be("Bulk copy to 'dbo.Posts' failed. See InnerException for details.");
        BulkCopyErrorMessages.BulkCopyFailed("dbo.Posts", "SQL Server", "547")
            .Should().Be("Bulk copy to 'dbo.Posts' failed (SQL Server error 547). See InnerException for details.");
        BulkCopyErrorMessages.BulkCopyFailed("POSTS", "Oracle", "ORA-00001")
            .Should().Be("Bulk copy to 'POSTS' failed (Oracle error ORA-00001). See InnerException for details.");
    }

    [Test]
    public void Builders_ShouldSanitiseTheirArguments_WhenTheyContainControlCharactersOrAreTooLong()
    {
        BulkCopyErrorMessages.BulkCopyFailed("A\r\nB", "SQL\nServer", "5\t47")
            .Should().Be("Bulk copy to 'A??B' failed (SQL?Server error 5?47). See InnerException for details.");
        BulkCopyErrorMessages.SourceColumnNotFound("Col\r\nX")
            .Should().Be("The mapped source column 'Col??X' does not exist in the data being copied.");
        BulkCopyErrorMessages.SourceColumnNotFound(new string('x', 500)).Length.Should().BeLessThan(250);
    }

    [Test]
    public void BulkCopyFailed_ShouldNotThrow_WhenArgumentsAreNull()
    {
        BulkCopyErrorMessages.BulkCopyFailed(null).Should().Be("Bulk copy to '' failed. See InnerException for details.");
        BulkCopyErrorMessages.BulkCopyFailed(null, null, null).Should().StartWith("Bulk copy to '' failed ( error ).");
    }
}
