using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Expediente.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AutoservicioEmpleado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "actualizacion_pedida_en",
                schema: "expediente",
                table: "empleados",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cambio_propuesto",
                schema: "expediente",
                table: "empleados",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "datos_confirmados_en",
                schema: "expediente",
                table: "empleados",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_empleados_documento",
                schema: "expediente",
                table: "empleados",
                column: "documento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_empleados_documento",
                schema: "expediente",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "actualizacion_pedida_en",
                schema: "expediente",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "cambio_propuesto",
                schema: "expediente",
                table: "empleados");

            migrationBuilder.DropColumn(
                name: "datos_confirmados_en",
                schema: "expediente",
                table: "empleados");
        }
    }
}
