using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TCMD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstructorAccountLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InstructorId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_InstructorId",
                table: "AspNetUsers",
                column: "InstructorId",
                unique: true,
                filter: "[InstructorId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Instructors_InstructorId",
                table: "AspNetUsers",
                column: "InstructorId",
                principalTable: "Instructors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Instructors_InstructorId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_InstructorId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "InstructorId",
                table: "AspNetUsers");
        }
    }
}
