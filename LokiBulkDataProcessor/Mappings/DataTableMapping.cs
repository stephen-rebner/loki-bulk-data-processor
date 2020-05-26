using Loki.BulkDataProcessor.Mappings.Interfaces;
using Loki.BulkDataProcessor.Mappings.MappingLogic;

namespace Loki.BulkDataProcessor.Mappings
{
    public abstract class DataTableMapping : AbstractMapping, IMapDataTableSource
    {
        public abstract string SourceTableName { get; }

        public DataTableMapping() : base (new DataTableMappingInfo())
        {
        }

        public IToDestination Map(string sourceColumn)
        {
            //ThrowIfDuplicateSourceColumn(sourceColumn);
            var mappingInfo = (DataTableMappingInfo)MappingInfo;

            mappingInfo.Map(sourceColumn);

            return MappingInfo;
        }

        //public IAsPrimaryKey ToDestinationColumn(string destinationColumnName)
        //{
        //    //if (string.IsNullOrWhiteSpace(destinationColumnName))
        //    //{
        //    //    throw new MappingException($"The mapping for the {SourceTableName} data table contains a null or empty destination column.");
        //    //}

        //    //if (ColumnMappings.ContainsValue(destinationColumnName))
        //    //{
        //    //    throw new MappingException($"The mapping for the {SourceTableName} data table contains duplicate destination columns.");
        //    //}

        //    //ColumnMappings.Add(_currentColumnName, destinationColumnName);

        //    return this;
        //}

        //public void AsPrimaryKey()
        //{

        //}
    }
}
