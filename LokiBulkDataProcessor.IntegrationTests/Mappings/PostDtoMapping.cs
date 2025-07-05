using Loki.BulkDataProcessor.Mappings;
using LokiBulkDataProcessor.IntegrationTests.TestModels.Dtos;

namespace LokiBulkDataProcessor.IntegrationTests.Mappings
{
    public class PostDtoMapping : ModelMapping<PostDto>
    {
        public PostDtoMapping()
        {
            Map(dto => dto.ATitle).ToDestinationColumn("Title");
            Map(dto => dto.ContentA).ToDestinationColumn("Content");
            Map(dto => dto.ABlogId).ToDestinationColumn("BlogId");
        }
    }
}
 