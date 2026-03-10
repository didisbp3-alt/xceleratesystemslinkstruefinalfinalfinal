using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIPSI16.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameLocationAndEmployerMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Username to Users (unique login handle)
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            // Add Location to Users (city/region for opportunity matching)
            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Add LatestEmployerMessage to JobApplications
            migrationBuilder.AddColumn<string>(
                name: "LatestEmployerMessage",
                table: "JobApplications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Username", table: "Users");
            migrationBuilder.DropColumn(name: "Location", table: "Users");
            migrationBuilder.DropColumn(name: "LatestEmployerMessage", table: "JobApplications");
        }
    }
}
