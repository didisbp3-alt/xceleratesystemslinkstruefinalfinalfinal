using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIPSI16.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionAndEmployerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add SubscriptionPlan to Users
            migrationBuilder.AddColumn<int>(
                name: "SubscriptionPlan",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Add EmployerRequestDocumentUrl to Users
            migrationBuilder.AddColumn<string>(
                name: "EmployerRequestDocumentUrl",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Add EmployerRequestNote to Users
            migrationBuilder.AddColumn<string>(
                name: "EmployerRequestNote",
                table: "Users",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            // Add ApplicantResponse to JobApplications
            migrationBuilder.AddColumn<byte>(
                name: "ApplicantResponse",
                table: "JobApplications",
                type: "tinyint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SubscriptionPlan", table: "Users");
            migrationBuilder.DropColumn(name: "EmployerRequestDocumentUrl", table: "Users");
            migrationBuilder.DropColumn(name: "EmployerRequestNote", table: "Users");
            migrationBuilder.DropColumn(name: "ApplicantResponse", table: "JobApplications");
        }
    }
}
