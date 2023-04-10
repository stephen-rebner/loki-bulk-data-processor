using Loki.BulkDataProcessor.DependancyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace LokiBulkDataProcessor.IntegrationTests
{
    public class Startup
    {
        private string _connectionString;
        IConfigurationRoot Configuration { get; }

        public Startup(string connectionString)
        {
            _connectionString = connectionString;
            
            var builder = new ConfigurationBuilder();

            Configuration = builder.Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLokiBulkDataProcessor(
                _connectionString, 
                Assembly.GetExecutingAssembly());
        }
    }
}
