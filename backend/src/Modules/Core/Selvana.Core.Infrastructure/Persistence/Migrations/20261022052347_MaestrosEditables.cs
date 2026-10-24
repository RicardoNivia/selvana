using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MaestrosEditables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "activo",
                schema: "core",
                table: "sedes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "editado_manualmente",
                schema: "core",
                table: "sedes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "nombre_nomina",
                schema: "core",
                table: "sedes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "activo",
                schema: "core",
                table: "centros_costo",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "editado_manualmente",
                schema: "core",
                table: "centros_costo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "nombre_nomina",
                schema: "core",
                table: "centros_costo",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "activo",
                schema: "core",
                table: "cargos",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "editado_manualmente",
                schema: "core",
                table: "cargos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "perfil_solicitado_en",
                schema: "core",
                table: "cargos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "activo",
                schema: "core",
                table: "areas",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "editado_manualmente",
                schema: "core",
                table: "areas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "nombre_nomina",
                schema: "core",
                table: "areas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Los datos ya importados se reconocen en la próxima sincronización por su nombre actual
            migrationBuilder.Sql("UPDATE core.sedes SET nombre_nomina = nombre WHERE nombre_nomina = '';");
            migrationBuilder.Sql("UPDATE core.areas SET nombre_nomina = nombre WHERE nombre_nomina = '';");
            migrationBuilder.Sql("UPDATE core.centros_costo SET nombre_nomina = nombre WHERE nombre_nomina = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "activo",
                schema: "core",
                table: "sedes");

            migrationBuilder.DropColumn(
                name: "editado_manualmente",
                schema: "core",
                table: "sedes");

            migrationBuilder.DropColumn(
                name: "nombre_nomina",
                schema: "core",
                table: "sedes");

            migrationBuilder.DropColumn(
                name: "activo",
                schema: "core",
                table: "centros_costo");

            migrationBuilder.DropColumn(
                name: "editado_manualmente",
                schema: "core",
                table: "centros_costo");

            migrationBuilder.DropColumn(
                name: "nombre_nomina",
                schema: "core",
                table: "centros_costo");

            migrationBuilder.DropColumn(
                name: "activo",
                schema: "core",
                table: "cargos");

            migrationBuilder.DropColumn(
                name: "editado_manualmente",
                schema: "core",
                table: "cargos");

            migrationBuilder.DropColumn(
                name: "perfil_solicitado_en",
                schema: "core",
                table: "cargos");

            migrationBuilder.DropColumn(
                name: "activo",
                schema: "core",
                table: "areas");

            migrationBuilder.DropColumn(
                name: "editado_manualmente",
                schema: "core",
                table: "areas");

            migrationBuilder.DropColumn(
                name: "nombre_nomina",
                schema: "core",
                table: "areas");
        }
    }
}
