using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrainingSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CorrectionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByStaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastUpdatedByStaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_AspNetUsers_CreatedByStaffUserId",
                        column: x => x.CreatedByStaffUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_AspNetUsers_LastUpdatedByStaffUserId",
                        column: x => x.LastUpdatedByStaffUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_TrainingSessions_TrainingSessionId",
                        column: x => x.TrainingSessionId,
                        principalTable: "TrainingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_CreatedByStaffUserId",
                table: "AttendanceRecords",
                column: "CreatedByStaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EnrollmentId_TrainingSessionId",
                table: "AttendanceRecords",
                columns: new[] { "EnrollmentId", "TrainingSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_LastUpdatedByStaffUserId",
                table: "AttendanceRecords",
                column: "LastUpdatedByStaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_TrainingSessionId_EnrollmentId",
                table: "AttendanceRecords",
                columns: new[] { "TrainingSessionId", "EnrollmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRecords");
        }
    }
}
