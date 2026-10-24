using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContratacionAprobada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "contratacion_aprobada_en",
                schema: "reclutamiento",
                table: "postulaciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contratacion_aprobada_por",
                schema: "reclutamiento",
                table: "postulaciones",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "proceso_vinculacion_id",
                schema: "reclutamiento",
                table: "postulaciones",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contratacion_aprobada_en",
                schema: "reclutamiento",
                table: "postulaciones");

            migrationBuilder.DropColumn(
                name: "contratacion_aprobada_por",
                schema: "reclutamiento",
                table: "postulaciones");

            migrationBuilder.DropColumn(
                name: "proceso_vinculacion_id",
                schema: "reclutamiento",
                table: "postulaciones");
        }
    }
}
