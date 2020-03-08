using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LokiBulkDataProcessor.IntegrationTests.Migrations
{
    public partial class UpdateTestStagingTableName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestDbModels");

            migrationBuilder.CreateTable(
                name: "TestStagingTableObjects",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StringColumn = table.Column<string>(nullable: true),
                    BoolColumn = table.Column<bool>(nullable: false),
                    DateColumn = table.Column<DateTime>(nullable: false),
                    NullableBoolColumn = table.Column<bool>(nullable: true),
                    NullableDateColumn = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestStagingTableObjects", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestStagingTableObjects");

            migrationBuilder.CreateTable(
                name: "TestDbModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BoolColumn = table.Column<bool>(type: "bit", nullable: false),
                    DateColumn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NullableBoolColumn = table.Column<bool>(type: "bit", nullable: true),
                    NullableDateColumn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StringColumn = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDbModels", x => x.Id);
                });
        }
    }
}
