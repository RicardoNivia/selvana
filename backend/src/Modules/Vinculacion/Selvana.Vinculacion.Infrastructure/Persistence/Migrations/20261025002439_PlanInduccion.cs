using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Vinculacion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanInduccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "induccion",
                schema: "vinculacion",
                table: "procesos",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "onboarding_cerrado_en",
                schema: "vinculacion",
                table: "procesos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "onboarding_cerrado_por",
                schema: "vinculacion",
                table: "procesos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "induccion",
                schema: "vinculacion",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "onboarding_cerrado_en",
                schema: "vinculacion",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "onboarding_cerrado_por",
                schema: "vinculacion",
                table: "procesos");
        }
    }
}
