# Loki Bulk Data Processor
An Asp.Net Core library which enables the fast bulk processing of data.

The Bulk Data Processor Class is available in the Loki.BulkDataProcessor namespace and can be instantiated by passing in the connection string into the constructor:

var bulkProcessor = new BulkProcessor(connectionString);

Alternatively, the Bulk Data Processor can take advantage of the inbuilt Asp.Net Core Dependancy Injection framework by adding the using Loki.BulkDataProcessor.DependancyInjection namespace into your StartUp.cs file, and adding the following code:

public void ConfigureServices(IServiceCollection services)
{
	services.AddLokiBulkDataProcessor(connectionString);
}

The Bulk Processor can then be injected into the Constructor of you class as follows:

private IBulkProcessor _bulkProcessor;

public ClassA(IBulkProcessor bulkProcessor)
{
	_bulkProcessor = bulkProcessor;
}

The Bulk Processor can then be executed by calling:

_bulkProcessor.SaveAsync(IEnumerable<YourModel>() models, string destinationTableName);

Or;

_bulkProcessor.SaveAsync(DataTable dataTable, string destinationTableName);

Column mappings are not yet available, so if you are passing an IEnumerable collection to the SaveAsync() method, the Property names on your model object must match the column names of your Database Table. The destinationTableName parameter must also match the table name you are saving to

For example, if the database table is called "TableA" and there are two columns ("ColumnA" (int), "ColumnB (NVarchar)"), the following model object would created:

public class ModelObject
{
	public int ColumnA { get; set; }
	public string ColumnB { get; set; }
} 

The model objects can then be initialised as normal, added to an IEnumerable collection, and passed to the SaveAsync() method as per the following example:

public async Task SaveModelsAsync()
{
	await _bulkProcessor.SaveAsync(models, "TableA");
}

If passing in a Data Table to the SaveAsync() method, the column names of the Data Table must also match the column names of the Database table. Given the example of TableA above, the column headers would be adding using the code below:

dataTable.Columns.Add(new DataColumn("ColumnA"));
dataTable.Columns.Add(new DataColumn("ColumnB"));

DataRows can then be added to the Data Table:

var dataRow = dataTable.NewRow();
dataRow["ColummA"] = id;
dataRow["ColumnB"] = stringValue;

dataTable.Rows.Add(dataRow);

The SaveAsync() method can then be called as follows:

public async Task SaveModelsAsync()
{
	await _bulkProcessor.SaveAsync(dataTable, "TableA");
}

There are two optional properties that can be set on the BulkProcessor class:

TimeOut (Default 30) - Number Of Seconds for the operation to complete before timing out;
BatchSize (Default 5000) - Number of rows in each batch sent to the server.
