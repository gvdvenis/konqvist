using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Konqvist.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _2_5_DistrictImportSourceUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DistrictImportSourceUrl",
                table: "GameTemplates",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "GameTemplates",
                keyColumn: "Id",
                keyValue: 1,
                column: "DistrictImportSourceUrl",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DistrictImportSourceUrl",
                table: "GameTemplates");
        }
    }
}
