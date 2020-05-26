using Loki.BulkDataProcessor.Mappings.MappingLogic;

namespace Loki.BulkDataProcessor.Mappings
{
    public class AbstractMapping
    {
        internal AbstractMappingInfo MappingInfo { get; }

        internal AbstractMapping(AbstractMappingInfo mappingInfo)
        {
            MappingInfo = mappingInfo;
        }

        //protected void ThrowIfDuplicateSourceColumn(string sourceColumn)
        //{
        //    if (ColumnMappings.ContainsKey(sourceColumn))
        //    {
        //        throw new MappingException($"The mapping contains a duplicate source column: {sourceColumn}");
        //    }
        //}
    }
}
