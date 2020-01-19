using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LokiBulkDataProcessor.IntegrationTests.Migrations
{
    public partial class InitITMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestDbModels",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StringColumn = table.Column<string>(nullable: true),
                    BoolColumn = table.Column<bool>(nullable: false),
                    DateColumn = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDbModels", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestDbModels");
        }
    }
}
