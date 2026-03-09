using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Konqvist.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _1_2_InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TotalRounds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 4),
                    LocationUpdateIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 30),
                    MinLocationUpdateIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 5),
                    VotingDurationSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 30),
                    PredictionBonusPoints = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 150),
                    VoteTimeoutPenalty = table.Column<int>(type: "INTEGER", nullable: false),
                    DistrictCaptureRadiusMeters = table.Column<double>(type: "REAL", nullable: false, defaultValue: 50.0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DistrictTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GeoJson = table.Column<string>(type: "TEXT", nullable: false),
                    TriggerLat = table.Column<double>(type: "REAL", nullable: false),
                    TriggerLng = table.Column<double>(type: "REAL", nullable: false),
                    TriggerRadiusMeters = table.Column<double>(type: "REAL", nullable: true),
                    Gold = table.Column<int>(type: "INTEGER", nullable: false),
                    Voters = table.Column<int>(type: "INTEGER", nullable: false),
                    Likes = table.Column<int>(type: "INTEGER", nullable: false),
                    Oil = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistrictTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistrictTemplates_GameTemplates_GameTemplateId",
                        column: x => x.GameTemplateId,
                        principalTable: "GameTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoundTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    RoundNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RoiResource = table.Column<string>(type: "TEXT", nullable: false),
                    Stake = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundTemplates_GameTemplates_GameTemplateId",
                        column: x => x.GameTemplateId,
                        principalTable: "GameTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamTemplates_GameTemplates_GameTemplateId",
                        column: x => x.GameTemplateId,
                        principalTable: "GameTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    LoginToken = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerTemplates_TeamTemplates_TeamTemplateId",
                        column: x => x.TeamTemplateId,
                        principalTable: "TeamTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DistrictOwnershipSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoundSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    DistrictSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerTeamSessionId = table.Column<int>(type: "INTEGER", nullable: true),
                    Phase = table.Column<string>(type: "TEXT", nullable: false),
                    SnapshotTaken = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistrictOwnershipSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DistrictSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    DistrictTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentOwnerTeamSessionId = table.Column<int>(type: "INTEGER", nullable: true),
                    IsClaimedThisRound = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LastClaimedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistrictSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistrictSessions_DistrictTemplates_DistrictTemplateId",
                        column: x => x.DistrictTemplateId,
                        principalTable: "DistrictTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    RoundSessionId = table.Column<int>(type: "INTEGER", nullable: true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActorPlayerSessionId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Pending"),
                    CurrentPhase = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "WaitingForPlayers"),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CurrentRoundSessionId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSessions_GameTemplates_GameTemplateId",
                        column: x => x.GameTemplateId,
                        principalTable: "GameTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayerSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsLoggedIn = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    LastSeen = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LocationLat = table.Column<double>(type: "REAL", nullable: true),
                    LocationLng = table.Column<double>(type: "REAL", nullable: true),
                    LocationUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerSessions_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerSessions_PlayerTemplates_PlayerTemplateId",
                        column: x => x.PlayerTemplateId,
                        principalTable: "PlayerTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalScore = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalGold = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalVoters = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalLikes = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalOil = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamSessions_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamSessions_TeamTemplates_TeamTemplateId",
                        column: x => x.TeamTemplateId,
                        principalTable: "TeamTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoundSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    RoundTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Gathering"),
                    VotingEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    VotingStartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    WinnerTeamSessionId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundSessions_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoundSessions_RoundTemplates_RoundTemplateId",
                        column: x => x.RoundTemplateId,
                        principalTable: "RoundTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoundSessions_TeamSessions_WinnerTeamSessionId",
                        column: x => x.WinnerTeamSessionId,
                        principalTable: "TeamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RoundSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoundSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Phase = table.Column<string>(type: "TEXT", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Gold = table.Column<int>(type: "INTEGER", nullable: false),
                    Voters = table.Column<int>(type: "INTEGER", nullable: false),
                    Likes = table.Column<int>(type: "INTEGER", nullable: false),
                    Oil = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotTaken = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundSnapshots_RoundSessions_RoundSessionId",
                        column: x => x.RoundSessionId,
                        principalTable: "RoundSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoundSnapshots_TeamSessions_TeamSessionId",
                        column: x => x.TeamSessionId,
                        principalTable: "TeamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoundSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    VotingTeamSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetTeamSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    VoteValue = table.Column<int>(type: "INTEGER", nullable: false),
                    IsAutocast = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CastAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Votes_RoundSessions_RoundSessionId",
                        column: x => x.RoundSessionId,
                        principalTable: "RoundSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Votes_TeamSessions_TargetTeamSessionId",
                        column: x => x.TargetTeamSessionId,
                        principalTable: "TeamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Votes_TeamSessions_VotingTeamSessionId",
                        column: x => x.VotingTeamSessionId,
                        principalTable: "TeamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "GameTemplates",
                columns: new[] { "Id", "DistrictCaptureRadiusMeters", "LocationUpdateIntervalSeconds", "MinLocationUpdateIntervalSeconds", "Name", "PredictionBonusPoints", "TotalRounds", "VoteTimeoutPenalty", "VotingDurationSeconds" },
                values: new object[] { 1, 50.0, 30, 5, "Demo Game", 150, 2, 50, 30 });

            migrationBuilder.InsertData(
                table: "RoundTemplates",
                columns: new[] { "Id", "GameTemplateId", "RoiResource", "RoundNumber", "Stake" },
                values: new object[,]
                {
                    { 1, 1, "Gold", 1, "Gold grant doubles for the winning team this round." },
                    { 2, 1, "Voters", 2, "Winning team gains a voter momentum bonus." }
                });

            migrationBuilder.InsertData(
                table: "TeamTemplates",
                columns: new[] { "Id", "Color", "GameTemplateId", "Name" },
                values: new object[,]
                {
                    { 1, "#1E88E5", 1, "Alpha" },
                    { 2, "#E53935", 1, "Bravo" }
                });

            migrationBuilder.InsertData(
                table: "PlayerTemplates",
                columns: new[] { "Id", "LoginToken", "Role", "TeamTemplateId" },
                values: new object[,]
                {
                    { 1, "ALPHA_RUNNER", "Runner", 1 },
                    { 2, "ALPHA_LEADER", "TeamLeader", 1 },
                    { 3, "BRAVO_RUNNER", "Runner", 2 },
                    { 4, "BRAVO_LEADER", "TeamLeader", 2 },
                    { 5, "GM_DEMO", "GameMaster", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DistrictOwnershipSnapshots_DistrictSessionId",
                table: "DistrictOwnershipSnapshots",
                column: "DistrictSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_DistrictOwnershipSnapshots_OwnerTeamSessionId",
                table: "DistrictOwnershipSnapshots",
                column: "OwnerTeamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_DistrictOwnershipSnapshots_RoundSessionId_DistrictSessionId_Phase",
                table: "DistrictOwnershipSnapshots",
                columns: new[] { "RoundSessionId", "DistrictSessionId", "Phase" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DistrictSessions_CurrentOwnerTeamSessionId",
                table: "DistrictSessions",
                column: "CurrentOwnerTeamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_DistrictSessions_DistrictTemplateId",
                table: "DistrictSessions",
                column: "DistrictTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_DistrictSessions_GameSessionId",
                table: "DistrictSessions",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_DistrictTemplates_GameTemplateId",
                table: "DistrictTemplates",
                column: "GameTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_ActorPlayerSessionId",
                table: "GameEvents",
                column: "ActorPlayerSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_GameSessionId",
                table: "GameEvents",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_RoundSessionId",
                table: "GameEvents",
                column: "RoundSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_CurrentRoundSessionId",
                table: "GameSessions",
                column: "CurrentRoundSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_GameTemplateId",
                table: "GameSessions",
                column: "GameTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_GameSessionId",
                table: "PlayerSessions",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_PlayerTemplateId",
                table: "PlayerSessions",
                column: "PlayerTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTemplates_LoginToken",
                table: "PlayerTemplates",
                column: "LoginToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTemplates_TeamTemplateId",
                table: "PlayerTemplates",
                column: "TeamTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundSessions_GameSessionId",
                table: "RoundSessions",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundSessions_RoundTemplateId",
                table: "RoundSessions",
                column: "RoundTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundSessions_WinnerTeamSessionId",
                table: "RoundSessions",
                column: "WinnerTeamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundSnapshots_RoundSessionId_TeamSessionId_Phase",
                table: "RoundSnapshots",
                columns: new[] { "RoundSessionId", "TeamSessionId", "Phase" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoundSnapshots_TeamSessionId",
                table: "RoundSnapshots",
                column: "TeamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundTemplates_GameTemplateId_RoundNumber",
                table: "RoundTemplates",
                columns: new[] { "GameTemplateId", "RoundNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamSessions_GameSessionId",
                table: "TeamSessions",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamSessions_TeamTemplateId",
                table: "TeamSessions",
                column: "TeamTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTemplates_GameTemplateId_Name",
                table: "TeamTemplates",
                columns: new[] { "GameTemplateId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_RoundSessionId_VotingTeamSessionId",
                table: "Votes",
                columns: new[] { "RoundSessionId", "VotingTeamSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_TargetTeamSessionId",
                table: "Votes",
                column: "TargetTeamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_VotingTeamSessionId",
                table: "Votes",
                column: "VotingTeamSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_DistrictOwnershipSnapshots_DistrictSessions_DistrictSessionId",
                table: "DistrictOwnershipSnapshots",
                column: "DistrictSessionId",
                principalTable: "DistrictSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DistrictOwnershipSnapshots_RoundSessions_RoundSessionId",
                table: "DistrictOwnershipSnapshots",
                column: "RoundSessionId",
                principalTable: "RoundSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DistrictOwnershipSnapshots_TeamSessions_OwnerTeamSessionId",
                table: "DistrictOwnershipSnapshots",
                column: "OwnerTeamSessionId",
                principalTable: "TeamSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DistrictSessions_GameSessions_GameSessionId",
                table: "DistrictSessions",
                column: "GameSessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DistrictSessions_TeamSessions_CurrentOwnerTeamSessionId",
                table: "DistrictSessions",
                column: "CurrentOwnerTeamSessionId",
                principalTable: "TeamSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameEvents_GameSessions_GameSessionId",
                table: "GameEvents",
                column: "GameSessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameEvents_PlayerSessions_ActorPlayerSessionId",
                table: "GameEvents",
                column: "ActorPlayerSessionId",
                principalTable: "PlayerSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GameEvents_RoundSessions_RoundSessionId",
                table: "GameEvents",
                column: "RoundSessionId",
                principalTable: "RoundSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameSessions_RoundSessions_CurrentRoundSessionId",
                table: "GameSessions",
                column: "CurrentRoundSessionId",
                principalTable: "RoundSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameSessions_RoundSessions_CurrentRoundSessionId",
                table: "GameSessions");

            migrationBuilder.DropTable(
                name: "DistrictOwnershipSnapshots");

            migrationBuilder.DropTable(
                name: "GameEvents");

            migrationBuilder.DropTable(
                name: "RoundSnapshots");

            migrationBuilder.DropTable(
                name: "Votes");

            migrationBuilder.DropTable(
                name: "DistrictSessions");

            migrationBuilder.DropTable(
                name: "PlayerSessions");

            migrationBuilder.DropTable(
                name: "DistrictTemplates");

            migrationBuilder.DropTable(
                name: "PlayerTemplates");

            migrationBuilder.DropTable(
                name: "RoundSessions");

            migrationBuilder.DropTable(
                name: "RoundTemplates");

            migrationBuilder.DropTable(
                name: "TeamSessions");

            migrationBuilder.DropTable(
                name: "GameSessions");

            migrationBuilder.DropTable(
                name: "TeamTemplates");

            migrationBuilder.DropTable(
                name: "GameTemplates");
        }
    }
}
