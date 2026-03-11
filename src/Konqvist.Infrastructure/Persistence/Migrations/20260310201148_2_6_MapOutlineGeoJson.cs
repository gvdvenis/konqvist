using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Konqvist.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _2_6_MapOutlineGeoJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MapOutlineGeoJson",
                table: "GameTemplates",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "GameTemplates",
                keyColumn: "Id",
                keyValue: 1,
                column: "MapOutlineGeoJson",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MapOutlineGeoJson",
                table: "GameTemplates");
        }
    }
}
