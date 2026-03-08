using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIPSI16.Migrations
{
    /// <inheritdoc />
    public partial class AddJobApplicationExtendedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverLetter",
                table: "JobApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "JobApplications",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OpenToRemote",
                table: "JobApplications",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "JobApplications",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PortfolioUrl",
                table: "JobApplications",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedJobRoleIds",
                table: "JobApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearsOfExperience",
                table: "JobApplications",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CoverLetter", table: "JobApplications");
            migrationBuilder.DropColumn(name: "LinkedInUrl", table: "JobApplications");
            migrationBuilder.DropColumn(name: "OpenToRemote", table: "JobApplications");
            migrationBuilder.DropColumn(name: "PhoneNumber", table: "JobApplications");
            migrationBuilder.DropColumn(name: "PortfolioUrl", table: "JobApplications");
            migrationBuilder.DropColumn(name: "SelectedJobRoleIds", table: "JobApplications");
            migrationBuilder.DropColumn(name: "YearsOfExperience", table: "JobApplications");
        }
    }
}
