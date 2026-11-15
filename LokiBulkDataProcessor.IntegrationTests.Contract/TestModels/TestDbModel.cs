using System;

namespace LokiBulkDataProcessor.IntegrationTests.Contract.TestModels
{
    /// <summary>
    /// A row of TestDbModels. Deliberately has no <c>Id</c>: the table's Id is an identity column and the contract never
    /// writes identity columns, so no input shape built from this type can carry one. Providers key the table on a
    /// database-generated Id that the contract never reads.
    /// </summary>
    public class TestDbModel
    {
        public string StringColumn { get; set; }

        public bool BoolColumn { get; set; }

        public DateTime DateColumn { get; set; }

        public bool? NullableBoolColumn { get; set; }

        public DateTime? NullableDateColumn { get; set; }
    }
}
