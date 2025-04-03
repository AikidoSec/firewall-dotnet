using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EFCoreSqliteSampleApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pets",
                columns: table => new
                {
                    pet_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    pet_name = table.Column<string>(type: "TEXT", nullable: false),
                    owner = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Aikido Security")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pets", x => x.pet_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pets");
        }
    }
}
