﻿using Loki.BulkDataProcessor.Mappings;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Loki.BulkDataProcessor.InternalDbOperations.Interfaces
{
    public interface IBulkCopyCommand : IDisposable
    {
        Task WriteToServerAsync<T>(IEnumerable<T> dataToCopy, string[] propertyNames, string tableName) where T : class;

        Task WriteToServerAsync(DataTable dataToCopy, string tableName);

        void MapPrimaryKey(AbstractMapping mapping);

        void MapNonPrimaryKeyColumns(AbstractMapping mapping, IEnumerable<string> propertyNames);
    }
}
