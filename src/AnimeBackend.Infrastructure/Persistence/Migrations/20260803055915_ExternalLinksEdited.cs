using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AnimeBackend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExternalLinksEdited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExternalLinksEdited",
                table: "MediaItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalLinksEdited",
                table: "MediaItems");
        }
    }
}
