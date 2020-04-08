# Loki Bulk Data Processor
The Loki Bulk Data Processor is fast and simple Asp.Net Core library which processes data in bulk in SQL Server. It uses the SQL Bulk Copy class under the hood, and can process data model collections or data tables.

The package can be downloaded on Nuget by running the following command:

**Install-Package LokiBulkDataProcessor -Version 3.0.0**

Or by searching for "**Loki Bulk Data Processor**" in the "**Manage Nuget Packages for Solution**" window.

#### Performance Compared to Entity Framework

The following performance benchmarks were recorded against a laptop with an I7-4800MQ 2.70 Ghz CPU, 16 GB of RAM and a 256GB solid state hard drive. 

*Bulk Data Processor

<table>
  <thead>
    <tr>
      <th>Records Saved</th>
      <th>Time Taken</th>
      <th>RAM Consumed</th>
    </tr>
  </thead>
  <tbody>
    <td>100,000</td>
    <td>2.99 seconds</td> 
    <td>266MB</td> 
  </tbody>
</table>

*Entity Framework

<table>
  <thead>
    <tr>
      <th>Records Saved</th>
      <th>Time Taken</th>
      <th>RAM Consumed</th>
    </tr>
  </thead>
  <tbody>
    <td>100,000</td>
    <td>68.743 seconds</td> 
    <td>1GB+</td> 
  </tbody>
</table>

Note: Both results were recorded against an empty database, inserting records into a database table with 4 columns, which included 1 primary key and 1 foreign key.

The documentation for the library can be found in the Wiki: 

https://github.com/stephen-rebner/loki-bulk-data-processor/wiki

There are also code examples in the following Git Hub repository:

https://github.com/stephen-rebner/loki-bulk-data-processor-examples
