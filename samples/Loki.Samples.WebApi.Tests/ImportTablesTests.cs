using FluentAssertions;
using Loki.Samples.WebApi.Engines;
using Loki.Samples.WebApi.Errors;
using NUnit.Framework;

namespace Loki.Samples.WebApi.Tests;

public class ImportTablesTests
{
    [TestCase("customers", "Customers")]
    [TestCase("Products", "Products")]
    public void JsonDestinationFor_ShouldReturnTheTableNameFromCode_WhenTheTableIsOnTheAllowList(string routeValue, string expected)
    {
        ImportTables.JsonDestinationFor(routeValue).Should().Be(expected);
    }

    [TestCase("orders")]
    [TestCase("STG_PRODUCTS")]
    [TestCase("Products; DROP TABLE Products")]
    [TestCase("dbo.Products")]
    public void JsonDestinationFor_ShouldThrowNotFound_WhenTheTableIsNotOnTheAllowList(string routeValue)
    {
        Action resolve = () => ImportTables.JsonDestinationFor(routeValue);

        resolve.Should().Throw<RequestRejectedException>().Where(e => e.StatusCode == 404 && !e.Message.Contains(routeValue));
    }

    [Test]
    public void CountQueriesFor_ShouldIncludeTheStagingTable_OnlyWhenTheEngineIsOracle()
    {
        ImportTables.CountQueriesFor(DatabaseEngine.Oracle).Select(q => q.Key).Should().Contain(ImportTables.OracleProductsStaging);
        ImportTables.CountQueriesFor(DatabaseEngine.SqlServer).Select(q => q.Key).Should().NotContain(ImportTables.OracleProductsStaging);
    }
}
