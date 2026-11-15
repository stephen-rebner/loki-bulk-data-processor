using System.Threading.Tasks;
using Loki.BulkDataProcessor;
using NUnit.Framework;

namespace LokiBulkDataProcessor.IntegrationTests.Contract;

/// <summary>The factory resolves the provider registered by the provider's own builder extension.</summary>
public abstract partial class BulkProcessorContractTests
{
    [Test]
    public async Task FactoryCreate_ShouldReturnAWorkingProcessor_WhenTheProviderNameDiffersInCase()
    {
        IBulkProcessor processor = Factory.Create(ProviderName.ToUpperInvariant(), ConnectionString);

        await processor.SaveAsync(ContractRows.TwoTestDbModels(), TestDbModelsTable);

        await TheTestDbModelValuesShouldBe("One", "Two");
    }
}
