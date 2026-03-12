using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Anime.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Profiles",
                newName: "NickName");

            migrationBuilder.InsertData(
                table: "Profiles",
                columns: new[] { "Id", "CreatedAt", "CreatorId", "ModifiedAt", "ModifierId", "NickName" },
                values: new object[,]
                {
                    { new Guid("7230b554-0e54-b8ee-f8e9-343e71f28176"), new DateTime(2026, 3, 12, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("7230b554-0e54-b8ee-f8e9-343e71f28176"), null, null, "system" },
                    { new Guid("c839e423-bbaa-7b5b-ab26-430ceda8f064"), new DateTime(2026, 3, 12, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("7230b554-0e54-b8ee-f8e9-343e71f28176"), null, null, "kotonai" }
                });

            migrationBuilder.InsertData(
                table: "Animes",
                columns: new[] { "Id", "CreatedAt", "CreatorId", "ModifiedAt", "ModifierId", "ProfileId", "Title" },
                values: new object[,]
                {
                    { new Guid("732ce9d6-4b0c-91a3-ecf8-3345ee27634c"), new DateTime(2026, 3, 12, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("7230b554-0e54-b8ee-f8e9-343e71f28176"), null, null, new Guid("c839e423-bbaa-7b5b-ab26-430ceda8f064"), "anime2" },
                    { new Guid("c4cef5a7-bcb8-6f10-2a0f-eda636a68620"), new DateTime(2026, 3, 12, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("7230b554-0e54-b8ee-f8e9-343e71f28176"), null, null, new Guid("c839e423-bbaa-7b5b-ab26-430ceda8f064"), "anime1" }
                });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "AnimeId", "CreatedAt", "CreatorId", "ModifiedAt", "ModifierId", "Name", "ProfileId" },
                values: new object[,]
                {
                    { new Guid("c2df00ac-c1c5-8711-0601-a37c60d31626"), new Guid("732ce9d6-4b0c-91a3-ecf8-3345ee27634c"), new DateTime(2026, 3, 12, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("7230b554-0e54-b8ee-f8e9-343e71f28176"), null, null, "tag3", new Guid("c839e423-bbaa-7b5b-ab26-430ceda8f064") },
                    { new Guid("cee3bae9-7a1d-0bc0-0b1a-a2fbddc50cfb"), new Guid("c4cef5a7-bcb8-6f10-2a0f-eda636a68620"), new DateTime(2026, 3, 12, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("7230b554-0e54-b8ee-f8e9-343e71f28176"), null, null, "tag1", new Guid("c839e423-bbaa-7b5b-ab26-430ceda8f064") },
                    { new Guid("d8f72af3-b1e6-679f-a63a-f85e5e7b8a82"), new Guid("c4cef5a7-bcb8-6f10-2a0f-eda636a68620"), new DateTime(2026, 3, 12, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("7230b554-0e54-b8ee-f8e9-343e71f28176"), null, null, "tag2", new Guid("c839e423-bbaa-7b5b-ab26-430ceda8f064") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Tags",
                keyColumn: "Id",
                keyValue: new Guid("c2df00ac-c1c5-8711-0601-a37c60d31626"));

            migrationBuilder.DeleteData(
                table: "Tags",
                keyColumn: "Id",
                keyValue: new Guid("cee3bae9-7a1d-0bc0-0b1a-a2fbddc50cfb"));

            migrationBuilder.DeleteData(
                table: "Tags",
                keyColumn: "Id",
                keyValue: new Guid("d8f72af3-b1e6-679f-a63a-f85e5e7b8a82"));

            migrationBuilder.DeleteData(
                table: "Animes",
                keyColumn: "Id",
                keyValue: new Guid("732ce9d6-4b0c-91a3-ecf8-3345ee27634c"));

            migrationBuilder.DeleteData(
                table: "Animes",
                keyColumn: "Id",
                keyValue: new Guid("c4cef5a7-bcb8-6f10-2a0f-eda636a68620"));

            migrationBuilder.DeleteData(
                table: "Profiles",
                keyColumn: "Id",
                keyValue: new Guid("c839e423-bbaa-7b5b-ab26-430ceda8f064"));

            migrationBuilder.DeleteData(
                table: "Profiles",
                keyColumn: "Id",
                keyValue: new Guid("7230b554-0e54-b8ee-f8e9-343e71f28176"));

            migrationBuilder.RenameColumn(
                name: "NickName",
                table: "Profiles",
                newName: "Name");
        }
    }
}
