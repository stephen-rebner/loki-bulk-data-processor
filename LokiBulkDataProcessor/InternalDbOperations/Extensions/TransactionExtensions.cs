using System.Data;

namespace Loki.BulkDataProcessor.InternalDbOperations.Extensions
{
    internal static class TransactionExtensions
    {
        internal static void CommitIfUsingInternalTransaction(this IDbTransaction transaction, bool isUsingExternalTransaction)
        {
            if(!isUsingExternalTransaction)
            {
                transaction.Commit();
            }
        }

        internal static void DisposeIfUsingIntenralTransaction(this IDbTransaction transaction, bool isUsingExternalTransaction)
        {
            if(!isUsingExternalTransaction)
            {
                transaction.Dispose();
            }
        }
    }
}
