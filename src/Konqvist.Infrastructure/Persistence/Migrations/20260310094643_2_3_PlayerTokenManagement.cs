using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Konqvist.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _2_3_PlayerTokenManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PlayerTemplates",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "GmLoginToken",
                table: "GameTemplates",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "GameTemplates",
                keyColumn: "Id",
                keyValue: 1,
                column: "GmLoginToken",
                value: "GM57t7");

            migrationBuilder.UpdateData(
                table: "PlayerTemplates",
                keyColumn: "Id",
                keyValue: 1,
                column: "LoginToken",
                value: "AR15ee");

            migrationBuilder.UpdateData(
                table: "PlayerTemplates",
                keyColumn: "Id",
                keyValue: 2,
                column: "LoginToken",
                value: "ATC5y85");

            migrationBuilder.UpdateData(
                table: "PlayerTemplates",
                keyColumn: "Id",
                keyValue: 3,
                column: "LoginToken",
                value: "BR2q9L");

            migrationBuilder.UpdateData(
                table: "PlayerTemplates",
                keyColumn: "Id",
                keyValue: 4,
                column: "LoginToken",
                value: "BTC8k1M");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GmLoginToken",
                table: "GameTemplates");

            migrationBuilder.UpdateData(
                table: "PlayerTemplates",
                keyColumn: "Id",
                keyValue: 1,
                column: "LoginToken",
                value: "ALPHA_RUNNER");

            migrationBuilder.UpdateData(
                table: "PlayerTemplates",
                keyColumn: "Id",
                keyValue: 2,
                column: "LoginToken",
                value: "ALPHA_LEADER");

            migrationBuilder.UpdateData(
                table: "PlayerTemplates",
                keyColumn: "Id",
                keyValue: 3,
                column: "LoginToken",
                value: "BRAVO_RUNNER");

            migrationBuilder.UpdateData(
                table: "PlayerTemplates",
                keyColumn: "Id",
                keyValue: 4,
                column: "LoginToken",
                value: "BRAVO_LEADER");

            migrationBuilder.InsertData(
                table: "PlayerTemplates",
                columns: new[] { "Id", "LoginToken", "Role", "TeamTemplateId" },
                values: new object[] { 5, "GM_DEMO", "GameMaster", 1 });
        }
    }
}
