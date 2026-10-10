using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NflCoverage.Modules.PlayoffOdds.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261010010132_Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "odds");

        migrationBuilder.CreateTable(
            name: "Runs",
            schema: "odds",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Season = table.Column<int>(type: "integer", nullable: false),
                ThroughWeek = table.Column<int>(type: "integer", nullable: false),
                Iterations = table.Column<int>(type: "integer", nullable: false),
                ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Runs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TeamOdds",
            schema: "odds",
            columns: table => new
            {
                RunId = table.Column<Guid>(type: "uuid", nullable: false),
                TeamId = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                MeanWins = table.Column<double>(type: "double precision", nullable: false),
                MakePlayoffs = table.Column<double>(type: "double precision", nullable: false),
                WinDivision = table.Column<double>(type: "double precision", nullable: false),
                FirstSeed = table.Column<double>(type: "double precision", nullable: false),
                SeedProbabilities = table.Column<double[]>(type: "double precision[]", nullable: false),
                WinConference = table.Column<double>(type: "double precision", nullable: false),
                WinSuperBowl = table.Column<double>(type: "double precision", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeamOdds", x => new { x.RunId, x.TeamId });
                table.ForeignKey(
                    name: "FK_TeamOdds_Runs_RunId",
                    column: x => x.RunId,
                    principalSchema: "odds",
                    principalTable: "Runs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Runs_Season_ThroughWeek",
            schema: "odds",
            table: "Runs",
            columns: new[] { "Season", "ThroughWeek" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TeamOdds",
            schema: "odds");

        migrationBuilder.DropTable(
            name: "Runs",
            schema: "odds");
    }
}
