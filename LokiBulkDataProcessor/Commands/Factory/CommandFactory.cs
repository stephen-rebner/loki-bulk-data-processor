using Loki.BulkDataProcessor.Commands.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace Loki.BulkDataProcessor.Commands.Factory
{
    internal class CommandFactory : ICommandFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public CommandFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IBulkModelsCommand NewBulkCopyModelsCommand()
        {
            return _serviceProvider.GetService<IBulkModelsCommand>();
        }

        public IBulkDataTableCommand NewBulkCopyDataTableCommand()
        {
            return _serviceProvider.GetServices<IBulkDataTableCommand>()
                .First(service => service.GetType() == typeof(BulkCopyDataTableCommand));
        }

        public IBulkDataTableCommand NewBulkUpdateDataTableCommand()
        {
            return _serviceProvider.GetServices<IBulkDataTableCommand>()
                 .First(service => service.GetType() == typeof(BulkUpdateDataTableCommand));
        }

        public IBulkDataTableCommand NewBulkCopyDataTableToTempTable()
        {
            return _serviceProvider.GetServices<IBulkDataTableCommand>()
                 .First(service => service.GetType() == typeof(BulkCopyDataTableToTempTable));
        }

        public ICreateTempTableCommand NewCreateTempTableCommand()
        {
            return _serviceProvider.GetService<ICreateTempTableCommand>();
        }

        public IDropTempTableCommand NewDropTempTableCommand()
        {
            return _serviceProvider.GetService<IDropTempTableCommand>();
        }
    }
}
