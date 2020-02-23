using Loki.BulkDataProcessor.Commands.Factory.Interface;
using Loki.BulkDataProcessor.Commands.Interfaces;
using System;

namespace Loki.BulkDataProcessor.Commands.Factory
{
    public class BulkCommandFactory : IBulkCommandFactory
    {
        public IBulkCommand NewCommand<T>() where T : IBulkCommand
        {
            return (IBulkCommand)Activator.CreateInstance(typeof(T));
        }
    }
}
