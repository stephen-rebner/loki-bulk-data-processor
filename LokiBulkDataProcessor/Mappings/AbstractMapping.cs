using System.Collections.Generic;

namespace Loki.BulkDataProcessor.Mappings
{
    public class AbstractMapping
    {
        internal Dictionary<string, string> ColumnMappings { get; }

        protected AbstractMapping()
        {
            ColumnMappings = new Dictionary<string, string>();
        }
    }
}
