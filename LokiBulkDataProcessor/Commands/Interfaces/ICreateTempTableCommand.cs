namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface ICreateTempTableCommand
    {
        void Execute(string destinationTableName);
    }
}
