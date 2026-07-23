using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Imagetextextraction.Backend.Migrations
{
    /// <inheritdoc />
    public partial class SwitchToScannedDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Medications");

            migrationBuilder.DropTable(
                name: "Prescriptions");

            migrationBuilder.CreateTable(
                name: "ScannedDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    PersonName = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    DocumentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtractedText = table.Column<string>(type: "text", nullable: false),
                    ExtractedJson = table.Column<string>(type: "text", nullable: true),
                    ImagePath = table.Column<string>(type: "text", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScannedDocuments", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScannedDocuments");

            migrationBuilder.CreateTable(
                name: "Prescriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicName = table.Column<string>(type: "text", nullable: true),
                    ClinicPhone = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateOfVisit = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Diagnosis = table.Column<string>(type: "text", nullable: true),
                    DoctorName = table.Column<string>(type: "text", nullable: true),
                    DoctorRegistration = table.Column<string>(type: "text", nullable: true),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    ExtractedJson = table.Column<string>(type: "text", nullable: true),
                    ImagePath = table.Column<string>(type: "text", nullable: true),
                    MarkdownText = table.Column<string>(type: "text", nullable: false),
                    PatientAge = table.Column<string>(type: "text", nullable: true),
                    PatientGender = table.Column<string>(type: "text", nullable: true),
                    PatientName = table.Column<string>(type: "text", nullable: true),
                    PatientWeight = table.Column<string>(type: "text", nullable: true),
                    RawText = table.Column<string>(type: "text", nullable: false),
                    UploadDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadDay = table.Column<int>(type: "integer", nullable: false),
                    UploadMonth = table.Column<int>(type: "integer", nullable: false),
                    UploadTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    UploadYear = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Medications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrescriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AiExplanation = table.Column<string>(type: "text", nullable: true),
                    Dosage = table.Column<string>(type: "text", nullable: false),
                    Duration = table.Column<string>(type: "text", nullable: false),
                    Frequency = table.Column<string>(type: "text", nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Medications_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Medications_PrescriptionId",
                table: "Medications",
                column: "PrescriptionId");
        }
    }
}
