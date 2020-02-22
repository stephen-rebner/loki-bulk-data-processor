using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.InternalDbOperations.Interfaces;
using Loki.BulkDataProcessor.Utils.Reflection;
using System;
using System.Linq;
using System.Text;

namespace Loki.BulkDataProcessor.InternalDbOperations
{
    public class TempTable : ITempTable
    {

        #region Private Members

        private const string TempTableName = "#TempTable";
        private readonly string CreateTableStatement = $"CREATE TABLE { TempTableName }";
        private readonly string DropTableStatement = $"DROP TABLE { TempTableName }";
        private readonly ISqlServerCommand _sqlServerCommand;
        private readonly string _connectionString;
        private readonly string _commandText;
        private readonly int _commandTimeout;

        #endregion


        #region Constructors

        public TempTable(ISqlServerCommand sqlServerCommand, 
            string connectionString, 
            string commandText,
            int commandTimeout)
        {
            _sqlServerCommand = sqlServerCommand;
            _connectionString = connectionString;
            _commandText = commandText;
            _commandTimeout = commandTimeout;
        }

        #endregion


        #region Temp Table Operations

        public void Create(Type dataModelType)
        {
            var sqlBuilder = new StringBuilder();

            sqlBuilder.AppendFormat(CreateTableStatement, TempTableName);
            sqlBuilder.Append("(");

            var properties = dataModelType.GetPublicProperties();

            foreach (var property in properties)
            {
                if(!property.Equals(properties.LastOrDefault()))
                {
                    sqlBuilder.Append($"{ property.Name } ,");
                }
                else
                {
                    sqlBuilder.Append($"{ property.Name }");
                }
            }

            sqlBuilder.Append(")");

            _sqlServerCommand.Execute(_connectionString, _commandText, _commandTimeout);
        }

        public void Drop()
        {

        }

        #endregion
    }
}
