using System.Data;
using Loki.Samples.WebApi.Imports;

namespace Loki.Samples.WebApi.Staging;

/// <summary>Generated rows for STG_PRODUCTS, tagged with one import's batch id.</summary>
internal static class StagedProductRows
{
    /// <summary>
    /// A DataTable without a TableName, so the destination "STG_PRODUCTS" is the mapping key; no DataMapping has that key, so the
    /// columns map 1:1 by name. <paramref name="includeDuplicate"/> repeats the first SKU in the last row: the staging table accepts
    /// it (it has no keys), and Products' unique key rejects it when the rows move on. The caller disposes the table.
    /// </summary>
    public static DataTable Create(string batchId, int count, bool includeDuplicate)
    {
        DataTable table = new();
        table.Columns.Add("BatchId", typeof(string));
        table.Columns.Add("Sku", typeof(string));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Price", typeof(decimal));
        string run = ProductImportRows.NewRunId();
        for (int i = 1; i <= count; i++)
        {
            int skuNumber = includeDuplicate && i == count && count > 1 ? 1 : i;
            table.Rows.Add(batchId, $"S-{run}-{skuNumber:D6}", $"Staged product {i}", ProductImportRows.PriceOf(i));
        }

        return table;
    }
}
