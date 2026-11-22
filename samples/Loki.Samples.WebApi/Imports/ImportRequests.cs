namespace Loki.Samples.WebApi.Imports;

/// <summary>One customer in the body of <c>POST /api/{engine}/customers</c>.</summary>
internal sealed record CustomerRequest(string? Name, string? Email);

/// <summary>The body of <c>POST /api/sqlserver/customers-with-orders</c>.</summary>
internal sealed record CustomerWithOrdersRequest(string? Name, string? Email, int OrderCount, bool SimulateFailure);

/// <summary>The answer of the import endpoints: the Loki provider used, the table and the rows sent (null for JSON).</summary>
internal sealed record ImportResult(string Provider, string Table, int? Rows);

/// <summary>The answer of <c>POST /api/sqlserver/customers-with-orders</c>.</summary>
internal sealed record CustomerWithOrdersResult(int CustomerId, int Orders);

/// <summary>The answer of <c>POST /api/oracle/products/staging-import</c>.</summary>
internal sealed record StagingImportResult(string BatchId, int StagedRows, int MovedRows);
