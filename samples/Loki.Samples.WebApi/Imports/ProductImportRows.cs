using System.Data;
using Loki.Samples.WebApi.Mappings;

namespace Loki.Samples.WebApi.Imports;

/// <summary>
/// Generated product rows in the shape of a typical import file: columns named ImportSku, ImportName and ImportPrice, mapped to
/// the Products columns by <see cref="ProductImportMapping"/>. The SKUs are unique per call, so repeated requests do not collide
/// with Products' unique key.
/// </summary>
internal static class ProductImportRows
{
    /// <summary>A DataTable whose <see cref="DataTable.TableName"/> is the mapping key "ProductImport". The caller disposes it.</summary>
    public static DataTable Create(int count)
    {
        // TableName picks the DataMapping (README "Mappings": the source name is DataTable.TableName when it is not blank).
        DataTable table = new(ProductImportMapping.Source);
        table.Columns.Add("ImportSku", typeof(string));
        table.Columns.Add("ImportName", typeof(string));
        table.Columns.Add("ImportPrice", typeof(decimal));   // typed columns: Oracle does not convert strings as SqlBulkCopy does
        string run = NewRunId();
        for (int i = 1; i <= count; i++)
        {
            table.Rows.Add($"P-{run}-{i:D6}", $"Imported product {i}", PriceOf(i));
        }

        return table;
    }

    public static decimal PriceOf(int i) => Math.Round(1m + (i % 997) * 0.37m, 2);

    public static string NewRunId() => Guid.NewGuid().ToString("N")[..12];
}
