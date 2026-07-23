using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Imagetextextraction.Backend.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "ScannedDocuments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "ScannedDocuments",
                type: "text",
                nullable: true);
        }
    }
}
