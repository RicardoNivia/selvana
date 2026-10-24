using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Vinculacion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AfiliacionesYAlta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "afiliaciones",
                schema: "vinculacion",
                table: "procesos",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "alta",
                schema: "vinculacion",
                table: "procesos",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "empleado_creado_en",
                schema: "vinculacion",
                table: "procesos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "empleado_creado_por",
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
                name: "afiliaciones",
                schema: "vinculacion",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "alta",
                schema: "vinculacion",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "empleado_creado_en",
                schema: "vinculacion",
                table: "procesos");

            migrationBuilder.DropColumn(
                name: "empleado_creado_por",
                schema: "vinculacion",
                table: "procesos");
        }
    }
}
