using Loki.BulkDataProcessor.DependancyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace LokiBulkDataProcessor.IntegrationTests
{
    public class Startup
    {
        IConfigurationRoot Configuration { get; }

        public Startup()
        {
            var builder = new ConfigurationBuilder();

            Configuration = builder.Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLokiBulkDataProcessor(
                "Server=(local);Database=IntegrationTestsDb;Trusted_Connection=True;MultipleActiveResultSets=true", 
                Assembly.GetExecutingAssembly());
        }
    }
}
