using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Konqvist.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _2_7_DefaultDistrictCaptureRadius15 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "DistrictCaptureRadiusMeters",
                table: "GameTemplates",
                type: "REAL",
                nullable: false,
                defaultValue: 15.0,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldDefaultValue: 50.0);

            migrationBuilder.UpdateData(
                table: "GameTemplates",
                keyColumn: "Id",
                keyValue: 1,
                column: "DistrictCaptureRadiusMeters",
                value: 15.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "DistrictCaptureRadiusMeters",
                table: "GameTemplates",
                type: "REAL",
                nullable: false,
                defaultValue: 50.0,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldDefaultValue: 15.0);

            migrationBuilder.UpdateData(
                table: "GameTemplates",
                keyColumn: "Id",
                keyValue: 1,
                column: "DistrictCaptureRadiusMeters",
                value: 50.0);
        }
    }
}
