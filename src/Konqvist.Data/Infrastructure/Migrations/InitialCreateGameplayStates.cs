using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Konqvist.Data.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateGameplayStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameplayStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GameDefinitionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameplayStates", x => x.Id);
                    table.CheckConstraint("CK_GameplayStates_Payload_IsJson", "[ISJSON]([Payload]) = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameplayStates_Slot_GameDefinitionId",
                table: "GameplayStates",
                columns: new[] { "Slot", "GameDefinitionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameplayStates");
        }
    }
}
