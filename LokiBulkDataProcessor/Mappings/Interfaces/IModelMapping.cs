using System;
using System.Collections.Generic;
using System.Text;

namespace Loki.BulkDataProcessor.Mappings.Interfaces
{
    public interface IModelMapping
    {
        public Type SourceType { get; }
    }
}
