using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIPSI16.Migrations
{
    /// <inheritdoc />
    public partial class FixJobApplicationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the UserId1 shadow-property column if it was ever created by EF
            migrationBuilder.Sql(@"
                IF COL_LENGTH('JobApplications', 'UserId1') IS NOT NULL
                BEGIN
                    IF EXISTS (SELECT 1 FROM sys.indexes i
                               JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                               JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                               WHERE OBJECT_NAME(i.object_id) = 'JobApplications' AND c.name = 'UserId1')
                    BEGIN
                        DECLARE @ixName NVARCHAR(200);
                        SELECT @ixName = i.name
                        FROM sys.indexes i
                        JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                        JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                        WHERE OBJECT_NAME(i.object_id) = 'JobApplications' AND c.name = 'UserId1';
                        EXEC('DROP INDEX [' + @ixName + '] ON [JobApplications]');
                    END
                    ALTER TABLE [JobApplications] DROP COLUMN [UserId1];
                END
            ");

            // Add extended application fields idempotently
            migrationBuilder.Sql(@"
                IF COL_LENGTH('JobApplications', 'CoverLetter') IS NULL
                    ALTER TABLE [JobApplications] ADD [CoverLetter] nvarchar(max) NULL;
            ");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('JobApplications', 'LinkedInUrl') IS NULL
                    ALTER TABLE [JobApplications] ADD [LinkedInUrl] nvarchar(300) NULL;
            ");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('JobApplications', 'OpenToRemote') IS NULL
                    ALTER TABLE [JobApplications] ADD [OpenToRemote] bit NULL;
            ");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('JobApplications', 'PhoneNumber') IS NULL
                    ALTER TABLE [JobApplications] ADD [PhoneNumber] varchar(20) NULL;
            ");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('JobApplications', 'PortfolioUrl') IS NULL
                    ALTER TABLE [JobApplications] ADD [PortfolioUrl] nvarchar(300) NULL;
            ");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('JobApplications', 'SelectedJobRoleIds') IS NULL
                    ALTER TABLE [JobApplications] ADD [SelectedJobRoleIds] nvarchar(500) NULL;
            ");
            migrationBuilder.Sql(@"
                IF COL_LENGTH('JobApplications', 'YearsOfExperience') IS NULL
                    ALTER TABLE [JobApplications] ADD [YearsOfExperience] int NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('JobApplications', 'CoverLetter') IS NOT NULL
                    ALTER TABLE [JobApplications] DROP COLUMN [CoverLetter];
                IF COL_LENGTH('JobApplications', 'LinkedInUrl') IS NOT NULL
                    ALTER TABLE [JobApplications] DROP COLUMN [LinkedInUrl];
                IF COL_LENGTH('JobApplications', 'OpenToRemote') IS NOT NULL
                    ALTER TABLE [JobApplications] DROP COLUMN [OpenToRemote];
                IF COL_LENGTH('JobApplications', 'PhoneNumber') IS NOT NULL
                    ALTER TABLE [JobApplications] DROP COLUMN [PhoneNumber];
                IF COL_LENGTH('JobApplications', 'PortfolioUrl') IS NOT NULL
                    ALTER TABLE [JobApplications] DROP COLUMN [PortfolioUrl];
                IF COL_LENGTH('JobApplications', 'SelectedJobRoleIds') IS NOT NULL
                    ALTER TABLE [JobApplications] DROP COLUMN [SelectedJobRoleIds];
                IF COL_LENGTH('JobApplications', 'YearsOfExperience') IS NOT NULL
                    ALTER TABLE [JobApplications] DROP COLUMN [YearsOfExperience];
            ");
        }
    }
}
