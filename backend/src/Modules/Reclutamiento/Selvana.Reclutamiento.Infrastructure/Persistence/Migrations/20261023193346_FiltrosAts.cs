using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FiltrosAts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "filtros",
                schema: "reclutamiento",
                table: "vacantes",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rescatada_en",
                schema: "reclutamiento",
                table: "postulaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rescatada_por",
                schema: "reclutamiento",
                table: "postulaciones",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "filtros",
                schema: "reclutamiento",
                table: "vacantes");

            migrationBuilder.DropColumn(
                name: "rescatada_en",
                schema: "reclutamiento",
                table: "postulaciones");

            migrationBuilder.DropColumn(
                name: "rescatada_por",
                schema: "reclutamiento",
                table: "postulaciones");
        }
    }
}
