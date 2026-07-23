using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Imagetextextraction.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTypeAndExtractedJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "Prescriptions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExtractedJson",
                table: "Prescriptions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "ExtractedJson",
                table: "Prescriptions");
        }
    }
}
