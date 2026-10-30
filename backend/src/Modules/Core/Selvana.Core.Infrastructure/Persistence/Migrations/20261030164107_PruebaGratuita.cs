using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PruebaGratuita : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "prueba_hasta",
                schema: "core",
                table: "empresas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "registrada_en",
                schema: "core",
                table: "empresas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sector",
                schema: "core",
                table: "empresas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "tamano",
                schema: "core",
                table: "empresas",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_empresas_nit",
                schema: "core",
                table: "empresas",
                column: "nit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_empresas_nit",
                schema: "core",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "prueba_hasta",
                schema: "core",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "registrada_en",
                schema: "core",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "sector",
                schema: "core",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "tamano",
                schema: "core",
                table: "empresas");
        }
    }
}
