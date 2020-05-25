using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;

namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface ISqlCommand
    {
        void Execute(string sqlCommandText, SqlConnection sqlConnection);
    }
}
