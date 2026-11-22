using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Mappings.MappingLogic;

namespace Loki.BulkDataProcessor.Mappings
{
    /// <summary>
    /// Maps source columns of a <c>DataTable</c>, <c>IDataReader</c> or JSON payload to destination columns. Subclass it, set
    /// <see cref="SourceTableName"/>, call <see cref="Map"/> in the constructor, and register the assembly with
    /// <c>LokiBulkDataProcessorOptions.AddMappingsFrom</c>.
    /// </summary>
    /// <remarks>
    /// Lookup rule: the mapping applies when <see cref="SourceTableName"/> equals the input's source name, which is the explicit
    /// <c>sourceName</c> argument of <c>SaveAsync(IDataReader, string, string, CancellationToken)</c>, otherwise a non-blank
    /// <c>DataTable.TableName</c>, otherwise the destination table name (also for JSON and readers without a source name).
    /// Two mappings with the same SourceTableName fail at startup.
    /// </remarks>
    public abstract class DataMapping : AbstractMapping, IMapDataTableSource
    {
        /// <summary>The source name this mapping applies to (see the lookup rule in the remarks). Must not be blank, and the override
        /// must return a constant (for example <c>=&gt; "PostImport"</c>): the base constructor reads it before the derived
        /// constructor runs, so it must not depend on fields set in a constructor.</summary>
        public abstract string SourceTableName { get; }

        /// <summary>Creates the mapping; call <see cref="Map"/> from the derived constructor.</summary>
        public DataMapping()
        {
            MappingInfo = new DataMappingInfo(SourceTableName);
        }

        /// <summary>
        /// Determines the data column to map as the source; follow it with <see cref="IToDestination.ToDestinationColumn"/>.
        /// </summary>
        /// <param name="sourceColumn">The column name in the data being copied.</param>
        /// <returns>The step that names the destination column.</returns>
        public IToDestination Map(string sourceColumn)
        {
            var mappingInfo = (DataMappingInfo)MappingInfo;

            mappingInfo.Map(sourceColumn);

            return mappingInfo;
        }
    }
}
