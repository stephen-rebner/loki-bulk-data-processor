namespace Loki.BulkDataProcessor.Commands.Interfaces
{
    public interface ISqlCommand
    {
        /// <summary>
        /// Executes a Non Query Command against the database
        /// </summary>
        /// <param name="sqlCommandText">The query to execute</param>
        void Execute(string sqlCommandText);
    }
}
