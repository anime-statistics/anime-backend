using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeBackend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiModelUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiModelUsages",
                columns: table => new
                {
                    ModelId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CallCount = table.Column<int>(type: "INTEGER", nullable: false),
                    InputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<long>(type: "INTEGER", nullable: false),
                    LastUsedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModelUsages", x => x.ModelId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiModelUsages");
        }
    }
}
