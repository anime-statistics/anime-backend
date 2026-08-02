using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AnimeBackend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SecondarySource = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TitleRussian = table.Column<string>(type: "TEXT", nullable: true),
                    TitleJapanese = table.Column<string>(type: "TEXT", nullable: true),
                    TitleEnglish = table.Column<string>(type: "TEXT", nullable: true),
                    EpisodesTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    VolumesTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    ChaptersTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceScore = table.Column<double>(type: "REAL", nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Synopsis = table.Column<string>(type: "TEXT", nullable: true),
                    Genres = table.Column<string>(type: "TEXT", nullable: false),
                    AiredFrom = table.Column<string>(type: "TEXT", nullable: true),
                    AiredTo = table.Column<string>(type: "TEXT", nullable: true),
                    PublishedFrom = table.Column<string>(type: "TEXT", nullable: true),
                    PublishedTo = table.Column<string>(type: "TEXT", nullable: true),
                    Rating = table.Column<string>(type: "TEXT", nullable: true),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    Authors = table.Column<string>(type: "TEXT", nullable: false),
                    Related = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalLinks = table.Column<string>(type: "TEXT", nullable: false),
                    FetchedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    SearchText = table.Column<string>(type: "TEXT", nullable: false),
                    UserScore = table.Column<double>(type: "REAL", nullable: true),
                    WatchedEpisodes = table.Column<int>(type: "INTEGER", nullable: false),
                    VolumesRead = table.Column<int>(type: "INTEGER", nullable: false),
                    ChaptersRead = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaId = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "media_item_tags",
                columns: table => new
                {
                    MediaItemsId = table.Column<string>(type: "TEXT", nullable: false),
                    TagsId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_item_tags", x => new { x.MediaItemsId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_media_item_tags_MediaItems_MediaItemsId",
                        column: x => x.MediaItemsId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_media_item_tags_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "Color", "Icon", "IsHidden", "Name", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f701"), "#22c55e", "pi-play", false, "Смотрю", 0 },
                    { new Guid("0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f702"), "#3b82f6", "pi-bookmark", false, "Запланировано", 1 },
                    { new Guid("0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f703"), "#8b5cf6", "pi-check-circle", false, "Просмотрено", 2 },
                    { new Guid("0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f704"), "#f59e0b", "pi-pause", false, "Отложено", 3 },
                    { new Guid("0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f705"), "#ef4444", "pi-times-circle", false, "Брошено", 4 },
                    { new Guid("0f1a2b3c-4d5e-4f60-8a91-b2c3d4e5f706"), "#06b6d4", "pi-replay", false, "Пересматриваю", 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_media_item_tags_TagsId",
                table: "media_item_tags",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_Type",
                table: "MediaItems",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_MediaId",
                table: "Notes",
                column: "MediaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_item_tags");

            migrationBuilder.DropTable(
                name: "Notes");

            migrationBuilder.DropTable(
                name: "MediaItems");

            migrationBuilder.DropTable(
                name: "Tags");
        }
    }
}
