using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPortfolio.Migrations
{
    /// <inheritdoc />
    public partial class AddActivitySync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActivitySyncedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_UserId_RepoName_ContributionType_ContributionDate",
                table: "Contributions",
                columns: new[] { "UserId", "RepoName", "ContributionType", "ContributionDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contributions_UserId_RepoName_ContributionType_ContributionDate",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "ActivitySyncedAt",
                table: "Users");
        }
    }
}
