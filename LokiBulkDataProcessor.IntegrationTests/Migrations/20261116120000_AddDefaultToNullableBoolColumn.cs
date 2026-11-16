using Microsoft.EntityFrameworkCore.Migrations;

namespace LokiBulkDataProcessor.IntegrationTests.Migrations
{
    /// <summary>
    /// Gives TestDbModels.NullableBoolColumn a DEFAULT of 1, so the KeepNulls contract test can tell an explicit NULL from a
    /// defaulted value (the Oracle schema has the same default). Raw SQL on purpose: the default is a test-schema detail that the
    /// EF model does not need, so the model and the snapshot are unchanged.
    /// </summary>
    public partial class AddDefaultToNullableBoolColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE TestDbModels ADD CONSTRAINT DF_TestDbModels_NullableBoolColumn DEFAULT 1 FOR NullableBoolColumn");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE TestDbModels DROP CONSTRAINT DF_TestDbModels_NullableBoolColumn");
        }
    }
}
