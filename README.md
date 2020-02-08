# Loki Bulk Data Processor
A Asp.Net Core library which enables the fast bulk processing of data.

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


