using Loki.BulkDataProcessor.Commands.Interfaces;
using Loki.BulkDataProcessor.Constants;
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

        private readonly ISqlCommand _sqlServerCommand;

        #endregion


        #region Constructors

        public TempTable(ISqlCommand sqlServerCommand)
        {
            _sqlServerCommand = sqlServerCommand;
        }

        #endregion


        #region Temp Table Operations

        /// <summary>
        /// Creates a temp table on the database
        /// </summary>
        public void Create(Type dataModelType)
        {
            var sqlBuilder = new StringBuilder();

            sqlBuilder.Append($"CREATE TABLE { DbConstants.TempTableName }");
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

            _sqlServerCommand.Execute(sqlBuilder.ToString());
        }

        /// <summary>
        /// Drops the temp table on the database
        /// </summary>
        public void Drop()
        {
            _sqlServerCommand.Execute($"DROP TABLE { DbConstants.TempTableName} ");
        }

        #endregion


    }
}
