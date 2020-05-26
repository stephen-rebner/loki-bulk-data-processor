using System;
using System.Collections.Generic;
using System.Text;

namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    public interface IToDestination
    {
        IAsPrimaryKey ToDestinationColumn(string destinationColumnName);
    }
}
