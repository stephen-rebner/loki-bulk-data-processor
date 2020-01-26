using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace LokiBulkDataProcessor.IntegrationTests.Migrations
{
    public partial class AddNullColumnsToTestDbModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NullableBoolColumn",
                table: "TestDbModels",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NullableDateColumn",
                table: "TestDbModels",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NullableBoolColumn",
                table: "TestDbModels");

            migrationBuilder.DropColumn(
                name: "NullableDateColumn",
                table: "TestDbModels");
        }
    }
}
