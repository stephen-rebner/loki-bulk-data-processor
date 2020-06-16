using Loki.BulkDataProcessor.Commands.Interfaces;
using System.Collections.Generic;
using System.Data;

namespace Loki.BulkDataProcessor.Commands.Factory
{
    public interface ICommandFactory
    {
        IBulkModelsCommand NewBulkCopyModelsCommand();

        IBulkDataTableCommand NewBulkCopyDataTableCommand();

        IBulkDataTableCommand NewBulkUpdateDataTableCommand();
    }
}
