using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NflCoverage.Modules.Ingestion.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261010010128_Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "ingestion");

        migrationBuilder.CreateTable(
            name: "ExternalIds",
            schema: "ingestion",
            columns: table => new
            {
                Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                EntityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                ExternalKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                InternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExternalIds", x => new { x.Provider, x.EntityType, x.ExternalKey });
            });

        migrationBuilder.CreateTable(
            name: "ImportRuns",
            schema: "ingestion",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RowsRead = table.Column<int>(type: "integer", nullable: false),
                RowsChanged = table.Column<int>(type: "integer", nullable: false),
                Error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImportRuns", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExternalIds_Provider_EntityType_InternalId",
            schema: "ingestion",
            table: "ExternalIds",
            columns: new[] { "Provider", "EntityType", "InternalId" });

        migrationBuilder.CreateIndex(
            name: "IX_ImportRuns_StartedAt",
            schema: "ingestion",
            table: "ImportRuns",
            column: "StartedAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ExternalIds",
            schema: "ingestion");

        migrationBuilder.DropTable(
            name: "ImportRuns",
            schema: "ingestion");
    }
}
