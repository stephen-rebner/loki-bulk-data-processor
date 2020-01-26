﻿// <auto-generated />
using System;
using LokiBulkDataProcessor.IntegrationTests.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LokiBulkDataProcessor.IntegrationTests.Migrations
{
    [DbContext(typeof(TestDbContext))]
    [Migration("20200126115850_AddNullColumnsToTestDbModel")]
    partial class AddNullColumnsToTestDbModel
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("LokiBulkDataProcessor.IntegrationTests.TestModel.TestDbModel", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<bool>("BoolColumn")
                        .HasColumnType("bit");

                    b.Property<DateTime>("DateColumn")
                        .HasColumnType("datetime2");

                    b.Property<bool?>("NullableBoolColumn")
                        .HasColumnType("bit");

                    b.Property<DateTime?>("NullableDateColumn")
                        .HasColumnType("datetime2");

                    b.Property<string>("StringColumn")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("TestDbModels");
                });
#pragma warning restore 612, 618
        }
    }
}
