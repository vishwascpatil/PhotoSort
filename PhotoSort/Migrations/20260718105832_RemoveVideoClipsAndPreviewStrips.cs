using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoSort.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVideoClipsAndPreviewStrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviewClipPath",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "PreviewFrameCount",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "PreviewStripGenerated",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "PreviewStripVersion",
                table: "Photos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviewClipPath",
                table: "Photos",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviewFrameCount",
                table: "Photos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PreviewStripGenerated",
                table: "Photos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PreviewStripVersion",
                table: "Photos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
