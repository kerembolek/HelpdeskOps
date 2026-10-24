using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpdeskOps.Migrations
{
    /// <inheritdoc />
    public partial class AddSignedDeviceReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "AcceptanceSignature",
                table: "AssignmentForms",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceivedById",
                table: "AssignmentForms",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceivedByName",
                table: "AssignmentForms",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ReceiverSignature",
                table: "AssignmentForms",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnCheckNote",
                table: "AssignmentForms",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnCondition",
                table: "AssignmentForms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnNote",
                table: "AssignmentForms",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReturnReason",
                table: "AssignmentForms",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnRequestedAt",
                table: "AssignmentForms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ReturnSignature",
                table: "AssignmentForms",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentForms_ReceivedById",
                table: "AssignmentForms",
                column: "ReceivedById");

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentForms_AspNetUsers_ReceivedById",
                table: "AssignmentForms",
                column: "ReceivedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssignmentForms_AspNetUsers_ReceivedById",
                table: "AssignmentForms");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentForms_ReceivedById",
                table: "AssignmentForms");

            migrationBuilder.DropColumn(
                name: "AcceptanceSignature",
                table: "AssignmentForms");

            migrationBuilder.DropColumn(
                name: "ReceivedById",
                table: "AssignmentForms");

            migrationBuilder.DropColumn(
                name: "ReceivedByName",
                table: "AssignmentForms");

            migrationBuilder.DropColumn(
                name: "ReceiverSignature",
                table: "AssignmentForms");

            migrationBuilder.DropColumn(
                name: "ReturnCheckNote",
                table: "AssignmentForms");

            migrationBuilder.DropColumn(
                name: "ReturnCondition",
                table: "AssignmentForms");

            migrationBuilder.DropColumn(
                name: "ReturnNote",
                table: "AssignmentForms");

            migrationBuilder.DropColumn(
                name: "ReturnReason",
                table: "AssignmentForms");

            migrationBuilder.DropColumn(
                name: "ReturnRequestedAt",
                table: "AssignmentForms");

            migrationBuilder.DropColumn(
                name: "ReturnSignature",
                table: "AssignmentForms");
        }
    }
}
