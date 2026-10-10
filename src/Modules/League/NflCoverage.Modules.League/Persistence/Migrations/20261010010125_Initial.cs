using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NflCoverage.Modules.League.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261010010125_Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "league");

        migrationBuilder.CreateTable(
            name: "Teams",
            schema: "league",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Nickname = table.Column<string>(type: "text", nullable: false),
                Conference = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                Division = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                PrimaryColor = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                SecondaryColor = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                LogoUrl = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Teams", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Games",
            schema: "league",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                Season = table.Column<int>(type: "integer", nullable: false),
                GameType = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                Week = table.Column<int>(type: "integer", nullable: false),
                Kickoff = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                HomeTeamId = table.Column<string>(type: "character varying(4)", nullable: false),
                AwayTeamId = table.Column<string>(type: "character varying(4)", nullable: false),
                HomeScore = table.Column<int>(type: "integer", nullable: true),
                AwayScore = table.Column<int>(type: "integer", nullable: true),
                Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                StatusDetail = table.Column<string>(type: "text", nullable: true),
                NeutralSite = table.Column<bool>(type: "boolean", nullable: false),
                HomeSpread = table.Column<double>(type: "double precision", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Games", x => x.Id);
                table.ForeignKey(
                    name: "FK_Games_Teams_AwayTeamId",
                    column: x => x.AwayTeamId,
                    principalSchema: "league",
                    principalTable: "Teams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Games_Teams_HomeTeamId",
                    column: x => x.HomeTeamId,
                    principalSchema: "league",
                    principalTable: "Teams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Games_AwayTeamId",
            schema: "league",
            table: "Games",
            column: "AwayTeamId");

        migrationBuilder.CreateIndex(
            name: "IX_Games_HomeTeamId",
            schema: "league",
            table: "Games",
            column: "HomeTeamId");

        migrationBuilder.CreateIndex(
            name: "IX_Games_Kickoff",
            schema: "league",
            table: "Games",
            column: "Kickoff");

        migrationBuilder.CreateIndex(
            name: "IX_Games_Season_Week",
            schema: "league",
            table: "Games",
            columns: new[] { "Season", "Week" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Games",
            schema: "league");

        migrationBuilder.DropTable(
            name: "Teams",
            schema: "league");
    }
}
