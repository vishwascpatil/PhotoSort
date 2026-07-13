using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoSort.Migrations
{
    /// <inheritdoc />
    public partial class AddPreviewClipPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviewClipPath",
                table: "Photos",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviewClipPath",
                table: "Photos");
        }
    }
}
