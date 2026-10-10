using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NflCoverage.Modules.Stats.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261010030955_Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "stats");

        migrationBuilder.CreateTable(
            name: "PlayerGames",
            schema: "stats",
            columns: table => new
            {
                PlayerId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                GameId = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                Season = table.Column<int>(type: "integer", nullable: false),
                Week = table.Column<int>(type: "integer", nullable: false),
                SeasonType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Team = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                Opponent = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                Extra = table.Column<string>(type: "jsonb", nullable: false),
                Stats_FantasyPointsPpr = table.Column<double>(type: "double precision", nullable: false),
                Stats_Defense_AssistedTackles = table.Column<int>(type: "integer", nullable: false),
                Stats_Defense_ForcedFumbles = table.Column<int>(type: "integer", nullable: false),
                Stats_Defense_Interceptions = table.Column<int>(type: "integer", nullable: false),
                Stats_Defense_PassesDefended = table.Column<int>(type: "integer", nullable: false),
                Stats_Defense_QbHits = table.Column<int>(type: "integer", nullable: false),
                Stats_Defense_Sacks = table.Column<double>(type: "double precision", nullable: false),
                Stats_Defense_SoloTackles = table.Column<int>(type: "integer", nullable: false),
                Stats_Defense_TacklesForLoss = table.Column<double>(type: "double precision", nullable: false),
                Stats_Kicking_FgAttempted = table.Column<int>(type: "integer", nullable: false),
                Stats_Kicking_FgLong = table.Column<int>(type: "integer", nullable: true),
                Stats_Kicking_FgMade = table.Column<int>(type: "integer", nullable: false),
                Stats_Kicking_PatAttempted = table.Column<int>(type: "integer", nullable: false),
                Stats_Kicking_PatMade = table.Column<int>(type: "integer", nullable: false),
                Stats_Passing_Attempts = table.Column<int>(type: "integer", nullable: false),
                Stats_Passing_Completions = table.Column<int>(type: "integer", nullable: false),
                Stats_Passing_Cpoe = table.Column<double>(type: "double precision", nullable: true),
                Stats_Passing_Epa = table.Column<double>(type: "double precision", nullable: false),
                Stats_Passing_Interceptions = table.Column<int>(type: "integer", nullable: false),
                Stats_Passing_Sacks = table.Column<int>(type: "integer", nullable: false),
                Stats_Passing_Touchdowns = table.Column<int>(type: "integer", nullable: false),
                Stats_Passing_Yards = table.Column<int>(type: "integer", nullable: false),
                Stats_Receiving_Epa = table.Column<double>(type: "double precision", nullable: false),
                Stats_Receiving_Receptions = table.Column<int>(type: "integer", nullable: false),
                Stats_Receiving_TargetShare = table.Column<double>(type: "double precision", nullable: true),
                Stats_Receiving_Targets = table.Column<int>(type: "integer", nullable: false),
                Stats_Receiving_Touchdowns = table.Column<int>(type: "integer", nullable: false),
                Stats_Receiving_Yards = table.Column<int>(type: "integer", nullable: false),
                Stats_Rushing_Carries = table.Column<int>(type: "integer", nullable: false),
                Stats_Rushing_Epa = table.Column<double>(type: "double precision", nullable: false),
                Stats_Rushing_Touchdowns = table.Column<int>(type: "integer", nullable: false),
                Stats_Rushing_Yards = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayerGames", x => new { x.PlayerId, x.GameId });
            });

        migrationBuilder.CreateTable(
            name: "Players",
            schema: "stats",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Position = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                PositionGroup = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                HeadshotUrl = table.Column<string>(type: "text", nullable: true),
                BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                College = table.Column<string>(type: "text", nullable: true),
                DraftYear = table.Column<int>(type: "integer", nullable: true),
                DraftRound = table.Column<int>(type: "integer", nullable: true),
                DraftPick = table.Column<int>(type: "integer", nullable: true),
                DraftTeam = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                RookieSeason = table.Column<int>(type: "integer", nullable: true),
                LastSeason = table.Column<int>(type: "integer", nullable: true),
                LatestTeam = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                Extra = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Players", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PlayerSeasons",
            schema: "stats",
            columns: table => new
            {
                PlayerId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                Season = table.Column<int>(type: "integer", nullable: false),
                SeasonType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Team = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                Games = table.Column<int>(type: "integer", nullable: false),
                Extra = table.Column<string>(type: "jsonb", nullable: false),
                Stats_FantasyPointsPpr = table.Column<double>(type: "double precision", nullable: false),
                Stats_Defense_AssistedTackles = table.Column<int>(type: "integer", nullable: false),
                Stats_Defense_ForcedFumbles = table.Column<int>(type: "integer", nullable: false),
                Stats_Defense_Interceptions = table.Column<int>(type: "integer", nullable: false),
                Stats_Defense_PassesDefended = table.Column<int>(type: "integer", nullable: false),
                Stats_Defense_QbHits = table.Column<int>(type: "integer", nullable: false),
                Stats_Defense_Sacks = table.Column<double>(type: "double precision", nullable: false),
                Stats_Defense_SoloTackles = table.Column<int>(type: "integer", nullable: false),
                Stats_Defense_TacklesForLoss = table.Column<double>(type: "double precision", nullable: false),
                Stats_Kicking_FgAttempted = table.Column<int>(type: "integer", nullable: false),
                Stats_Kicking_FgLong = table.Column<int>(type: "integer", nullable: true),
                Stats_Kicking_FgMade = table.Column<int>(type: "integer", nullable: false),
                Stats_Kicking_PatAttempted = table.Column<int>(type: "integer", nullable: false),
                Stats_Kicking_PatMade = table.Column<int>(type: "integer", nullable: false),
                Stats_Passing_Attempts = table.Column<int>(type: "integer", nullable: false),
                Stats_Passing_Completions = table.Column<int>(type: "integer", nullable: false),
                Stats_Passing_Cpoe = table.Column<double>(type: "double precision", nullable: true),
                Stats_Passing_Epa = table.Column<double>(type: "double precision", nullable: false),
                Stats_Passing_Interceptions = table.Column<int>(type: "integer", nullable: false),
                Stats_Passing_Sacks = table.Column<int>(type: "integer", nullable: false),
                Stats_Passing_Touchdowns = table.Column<int>(type: "integer", nullable: false),
                Stats_Passing_Yards = table.Column<int>(type: "integer", nullable: false),
                Stats_Receiving_Epa = table.Column<double>(type: "double precision", nullable: false),
                Stats_Receiving_Receptions = table.Column<int>(type: "integer", nullable: false),
                Stats_Receiving_TargetShare = table.Column<double>(type: "double precision", nullable: true),
                Stats_Receiving_Targets = table.Column<int>(type: "integer", nullable: false),
                Stats_Receiving_Touchdowns = table.Column<int>(type: "integer", nullable: false),
                Stats_Receiving_Yards = table.Column<int>(type: "integer", nullable: false),
                Stats_Rushing_Carries = table.Column<int>(type: "integer", nullable: false),
                Stats_Rushing_Epa = table.Column<double>(type: "double precision", nullable: false),
                Stats_Rushing_Touchdowns = table.Column<int>(type: "integer", nullable: false),
                Stats_Rushing_Yards = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayerSeasons", x => new { x.PlayerId, x.Season, x.SeasonType });
            });

        migrationBuilder.CreateTable(
            name: "TeamGames",
            schema: "stats",
            columns: table => new
            {
                Team = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                GameId = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                Season = table.Column<int>(type: "integer", nullable: false),
                Week = table.Column<int>(type: "integer", nullable: false),
                SeasonType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Opponent = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                Extra = table.Column<string>(type: "jsonb", nullable: false),
                Offense_Carries = table.Column<int>(type: "integer", nullable: false),
                Offense_Completions = table.Column<int>(type: "integer", nullable: false),
                Offense_FumblesLost = table.Column<int>(type: "integer", nullable: false),
                Offense_Interceptions = table.Column<int>(type: "integer", nullable: false),
                Offense_PassAttempts = table.Column<int>(type: "integer", nullable: false),
                Offense_PassingEpa = table.Column<double>(type: "double precision", nullable: false),
                Offense_PassingTds = table.Column<int>(type: "integer", nullable: false),
                Offense_PassingYards = table.Column<int>(type: "integer", nullable: false),
                Offense_RushingEpa = table.Column<double>(type: "double precision", nullable: false),
                Offense_RushingTds = table.Column<int>(type: "integer", nullable: false),
                Offense_RushingYards = table.Column<int>(type: "integer", nullable: false),
                Offense_SacksTaken = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeamGames", x => new { x.Team, x.GameId });
            });

        migrationBuilder.CreateTable(
            name: "TeamSeasons",
            schema: "stats",
            columns: table => new
            {
                Team = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                Season = table.Column<int>(type: "integer", nullable: false),
                SeasonType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Games = table.Column<int>(type: "integer", nullable: false),
                Penalties = table.Column<int>(type: "integer", nullable: false),
                PenaltyYards = table.Column<int>(type: "integer", nullable: false),
                Extra = table.Column<string>(type: "jsonb", nullable: false),
                Defense_ForcedFumbles = table.Column<int>(type: "integer", nullable: false),
                Defense_Interceptions = table.Column<int>(type: "integer", nullable: false),
                Defense_PassesDefended = table.Column<int>(type: "integer", nullable: false),
                Defense_QbHits = table.Column<int>(type: "integer", nullable: false),
                Defense_Sacks = table.Column<double>(type: "double precision", nullable: false),
                Defense_SoloTackles = table.Column<int>(type: "integer", nullable: false),
                Defense_TacklesForLoss = table.Column<double>(type: "double precision", nullable: false),
                Defense_Touchdowns = table.Column<int>(type: "integer", nullable: false),
                Offense_Carries = table.Column<int>(type: "integer", nullable: false),
                Offense_Completions = table.Column<int>(type: "integer", nullable: false),
                Offense_FumblesLost = table.Column<int>(type: "integer", nullable: false),
                Offense_Interceptions = table.Column<int>(type: "integer", nullable: false),
                Offense_PassAttempts = table.Column<int>(type: "integer", nullable: false),
                Offense_PassingEpa = table.Column<double>(type: "double precision", nullable: false),
                Offense_PassingTds = table.Column<int>(type: "integer", nullable: false),
                Offense_PassingYards = table.Column<int>(type: "integer", nullable: false),
                Offense_RushingEpa = table.Column<double>(type: "double precision", nullable: false),
                Offense_RushingTds = table.Column<int>(type: "integer", nullable: false),
                Offense_RushingYards = table.Column<int>(type: "integer", nullable: false),
                Offense_SacksTaken = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeamSeasons", x => new { x.Team, x.Season, x.SeasonType });
            });

        migrationBuilder.CreateIndex(
            name: "IX_PlayerGames_Season",
            schema: "stats",
            table: "PlayerGames",
            column: "Season");

        migrationBuilder.CreateIndex(
            name: "IX_Players_Name",
            schema: "stats",
            table: "Players",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_PlayerSeasons_Season_SeasonType",
            schema: "stats",
            table: "PlayerSeasons",
            columns: new[] { "Season", "SeasonType" });

        migrationBuilder.CreateIndex(
            name: "IX_PlayerSeasons_Team_Season",
            schema: "stats",
            table: "PlayerSeasons",
            columns: new[] { "Team", "Season" });

        migrationBuilder.CreateIndex(
            name: "IX_TeamGames_Season_SeasonType",
            schema: "stats",
            table: "TeamGames",
            columns: new[] { "Season", "SeasonType" });

        migrationBuilder.CreateIndex(
            name: "IX_TeamSeasons_Season_SeasonType",
            schema: "stats",
            table: "TeamSeasons",
            columns: new[] { "Season", "SeasonType" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PlayerGames",
            schema: "stats");

        migrationBuilder.DropTable(
            name: "Players",
            schema: "stats");

        migrationBuilder.DropTable(
            name: "PlayerSeasons",
            schema: "stats");

        migrationBuilder.DropTable(
            name: "TeamGames",
            schema: "stats");

        migrationBuilder.DropTable(
            name: "TeamSeasons",
            schema: "stats");
    }
}
