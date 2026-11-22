using Loki.Samples.WebApi.Errors;

namespace Loki.Samples.WebApi.Engines;

/// <summary>
/// The tables this API writes to, chosen by code. Loki does not decide who may write to which table (README: "Choosing the
/// database at run time"), so this allow-list does: a route value can only select one of these names, and a JSON payload's own
/// <c>tableName</c> is never used.
/// </summary>
internal static class ImportTables
{
    public const string Customers = "Customers";
    public const string Orders = "Orders";
    public const string Products = "Products";

    /// <summary>Oracle only: the direct-path staging table (see db/oracle/init.sql).</summary>
    public const string OracleProductsStaging = "STG_PRODUCTS";

    private static readonly Dictionary<string, string> JsonDestinationsByRouteValue = new(StringComparer.OrdinalIgnoreCase)
    {
        ["customers"] = Customers,
        ["products"] = Products
    };

    /// <summary>
    /// Constant count queries for the tables <c>GET /api/{engine}/counts</c> reports. Table names are never built from input.
    /// </summary>
    private static readonly Dictionary<string, string> CountQueries = new(StringComparer.Ordinal)
    {
        [Customers] = "SELECT COUNT(*) FROM Customers",
        [Orders] = "SELECT COUNT(*) FROM Orders",
        [Products] = "SELECT COUNT(*) FROM Products",
        [OracleProductsStaging] = "SELECT COUNT(*) FROM STG_PRODUCTS"
    };

    /// <summary>The route values <c>POST /api/{engine}/json/{table}</c> accepts.</summary>
    public static IReadOnlyCollection<string> JsonRouteValues => JsonDestinationsByRouteValue.Keys;

    /// <summary>The destination table for a JSON upload route value; 404 for any table that is not on the allow-list.</summary>
    public static string JsonDestinationFor(string routeValue) =>
        routeValue is not null && JsonDestinationsByRouteValue.TryGetValue(routeValue, out string? table)
            ? table
            : throw RequestRejectedException.NotFound($"This table does not accept JSON uploads. Use one of: {string.Join(", ", JsonRouteValues)}.");

    /// <summary>The tables and their constant count queries for <paramref name="engine"/>.</summary>
    public static IEnumerable<KeyValuePair<string, string>> CountQueriesFor(DatabaseEngine engine) =>
        CountQueries.Where(query => engine == DatabaseEngine.Oracle || query.Key != OracleProductsStaging);
}
