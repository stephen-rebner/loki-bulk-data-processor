using Loki.BulkDataProcessor.Commands.Abstract;
using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Context.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.Commands
{
    internal class BulkUpdateDataTableCommand : BaseBulkProcessorCommand, IBulkProcessorDataTableCommand
    {
        private readonly string _destinationTableName;

        public DataTable DataToCopy { get; set; }

        public BulkUpdateDataTableCommand(DataTable dataTable, string destinationTableName, IAppContext appContext)
            : base(appContext, destinationTableName)
        {
            DataToCopy = dataTable;
        }

        public Task Execute()
        {
            var mapping = _appContext.DataTableMappingCollection.GetMappingFor(_destinationTableName);

            throw new NotImplementedException();
        }
    }
}
