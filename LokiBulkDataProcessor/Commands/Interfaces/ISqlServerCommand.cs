using System;
using System.Collections.Generic;
using System.Text;

namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface ISqlServerCommand
    {
        void Execute(string connectionString, string commandText, int commandTimeout);
    }
}
