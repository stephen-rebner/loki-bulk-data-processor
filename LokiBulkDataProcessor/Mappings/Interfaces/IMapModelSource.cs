﻿using System;
using System.Linq.Expressions;

namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    internal interface IMapModelSource<TSource> where TSource : class
    {
        IToDestination Map<TKey>(Expression<Func<TSource, TKey>> keySelector);
    }
}
