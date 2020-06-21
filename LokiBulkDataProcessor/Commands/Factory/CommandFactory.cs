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
    }
}
